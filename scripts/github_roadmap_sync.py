#!/usr/bin/env python3
"""Mirror canonical Fadrio roadmap tickets into GitHub issues and milestones."""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ROADMAP_PATH = ROOT / "ROADMAP.md"
REPOSITORY = "ATAC-Helicopter/Fadrio"
OWNER = "ATAC-Helicopter"
MANAGED_MARKER = "Synced from ROADMAP.md"
TICKET_PATTERN = re.compile(
    r"^- \[(?P<done>[ xX])\] `(?P<id>FAD-\d{4}|BUG-\d{5}|REL-\d{5})` "
    r"`(?P<priority>P[012])` (?P<title>\S.*)$"
)
MILESTONES = {
    "M0": "PipeWire application identity spike and repository foundation.",
    "M0.2": "Real-application identity, isolated PipeWire restart, and sanitizer qualification.",
    "0.2": "Hardened evidence-based application identity and icon resolution.",
    "0.3": "Durable application preferences and stable mixer behavior.",
    "0.4": "Steam, Proton, and Wine identity.",
    "0.5": "Accessible production mixer and tray UI.",
    "0.6": "ALSA MIDI, Learn, bindings, pickup, and feedback.",
    "0.7": "Groups, manual profiles, and semantic roles.",
    "0.8": "Reliability, diagnostics, performance, and packaging qualification.",
    "0.9": "Public beta readiness candidate.",
    "1.0": "First stable release candidate and release gates.",
}
AREAS = {
    "M0": "Foundation",
    "M0.2": "Qualification",
    "0.2": "Identity",
    "0.3": "Persistence",
    "0.4": "Games",
    "0.5": "UI",
    "0.6": "Controllers",
    "0.7": "Profiles",
    "0.8": "Reliability",
    "0.9": "Delivery",
    "1.0": "Release",
}
LABELS = {
    "roadmap": ("5319E7", "Tracked by the canonical Fadrio roadmap"),
    "kind:fad": ("8250DF", "Product or engineering work item"),
    "kind:bug": ("D73A4A", "Observed incorrect behavior"),
    "kind:rel": ("B60205", "Release or qualification gate"),
    "priority:P0": ("B60205", "Release-blocking priority"),
    "priority:P1": ("D4A72C", "Primary milestone value"),
    "priority:P2": ("0E8A16", "Valuable deferrable work"),
}


@dataclass(frozen=True)
class Ticket:
    identifier: str
    title: str
    priority: str
    milestone: str
    details: tuple[str, ...]
    completed: bool

    @property
    def in_progress(self) -> bool:
        return not self.completed and any(
            detail.startswith("- Status: In progress") for detail in self.details
        )

    @property
    def issue_title(self) -> str:
        return f"{self.identifier}: {self.title.rstrip('.')}"

    @property
    def kind_label(self) -> str:
        if self.identifier.startswith("BUG-"):
            return "kind:bug"
        if self.identifier.startswith("REL-"):
            return "kind:rel"
        return "kind:fad"

    @property
    def body(self) -> str:
        details = "\n".join(self.details) or "- Scope and acceptance are defined by the roadmap title."
        return (
            f"{MANAGED_MARKER}\n\n"
            f"Work ID: `{self.identifier}`  \n"
            f"Milestone: `{self.milestone}`  \n"
            f"Priority: `{self.priority}`  \n"
            f"Area: `{AREAS[self.milestone]}`\n\n"
            f"{details}\n\n"
            "The canonical scope and delivery state live in `ROADMAP.md`."
        )


def parse_roadmap(text: str) -> list[Ticket]:
    tickets: list[Ticket] = []
    milestone = ""
    current: dict[str, object] | None = None
    in_fence = False

    def flush() -> None:
        nonlocal current
        if current is not None:
            tickets.append(Ticket(**current))
            current = None

    for line in text.splitlines():
        if line.startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        if line.startswith("## "):
            flush()
            candidate = line[3:].split(" —", 1)[0].strip()
            milestone = candidate if candidate in MILESTONES else ""
            continue
        match = TICKET_PATTERN.fullmatch(line)
        if match is not None:
            flush()
            if not milestone:
                raise ValueError(f"Ticket {match.group('id')} is outside a known milestone")
            current = {
                "identifier": match.group("id"),
                "title": match.group("title"),
                "priority": match.group("priority"),
                "milestone": milestone,
                "details": (),
                "completed": match.group("done").lower() == "x",
            }
            continue
        if current is not None and line.startswith("  - "):
            current["details"] = (*current["details"], line[2:])

    flush()
    return tickets


def run_gh(arguments: list[str]) -> str:
    executable = shutil.which("gh")
    if executable is None:
        raise RuntimeError("GitHub CLI (gh) is required")
    result = subprocess.run(
        [executable, *arguments],
        check=True,
        shell=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    return result.stdout.strip()


def ensure_labels() -> None:
    for name, (color, description) in LABELS.items():
        run_gh(["label", "create", name, "--repo", REPOSITORY, "--color", color,
                "--description", description, "--force"])
    for area in sorted(set(AREAS.values())):
        run_gh(["label", "create", f"area:{area.lower()}", "--repo", REPOSITORY,
                "--color", "C5DEF5", "--description", f"{area} work", "--force"])


def ensure_milestones() -> dict[str, int]:
    existing = json.loads(run_gh(["api", f"repos/{REPOSITORY}/milestones?state=all&per_page=100"]))
    numbers = {item["title"]: int(item["number"]) for item in existing}
    for title, description in MILESTONES.items():
        if title not in numbers:
            created = json.loads(run_gh([
                "api", "--method", "POST", f"repos/{REPOSITORY}/milestones",
                "-f", f"title={title}", "-f", f"description={description}",
            ]))
            numbers[title] = int(created["number"])
    return numbers


def issue_index() -> dict[str, dict[str, object]]:
    issues = json.loads(run_gh([
        "issue", "list", "--repo", REPOSITORY, "--state", "all", "--limit", "200",
        "--json", "number,title,url,body,state",
    ]))
    index: dict[str, dict[str, object]] = {}
    for issue in issues:
        identifier = issue["title"].split(":", 1)[0]
        if re.fullmatch(r"FAD-\d{4}|BUG-\d{5}|REL-\d{5}", identifier):
            index[identifier] = issue
    return index


def sync_issues(tickets: list[Ticket], milestone_numbers: dict[str, int]) -> list[str]:
    existing = issue_index()
    urls: list[str] = []
    for ticket in tickets:
        labels = ",".join([
            "roadmap", ticket.kind_label, f"priority:{ticket.priority}",
            f"area:{AREAS[ticket.milestone].lower()}",
        ])
        issue = existing.get(ticket.identifier)
        if issue is None:
            url = run_gh([
                "issue", "create", "--repo", REPOSITORY, "--title", ticket.issue_title,
                "--body", ticket.body, "--milestone", ticket.milestone, "--label", labels,
            ])
            number = int(url.rsplit("/", 1)[1])
        else:
            number = int(issue["number"])
            url = str(issue["url"])
            if str(issue.get("body") or "").startswith(MANAGED_MARKER):
                run_gh([
                    "api", "--method", "PATCH", f"repos/{REPOSITORY}/issues/{number}",
                    "-f", f"title={ticket.issue_title}", "--raw-field", f"body={ticket.body}",
                    "-F", f"milestone={milestone_numbers[ticket.milestone]}",
                ])
                run_gh([
                    "api", "--method", "POST", f"repos/{REPOSITORY}/issues/{number}/labels",
                    *[argument for label in labels.split(",") for argument in ("-f", f"labels[]={label}")],
                ])
        if ticket.completed:
            run_gh(["issue", "close", str(number), "--repo", REPOSITORY,
                    "--reason", "completed"])
        urls.append(url)
    return urls


def add_to_project(urls: list[str], project_number: int) -> None:
    for url in urls:
        run_gh(["project", "item-add", str(project_number), "--owner", OWNER, "--url", url])


def sync_project_fields(tickets: list[Ticket], project_number: int) -> None:
    project = json.loads(run_gh([
        "project", "view", str(project_number), "--owner", OWNER, "--format", "json",
    ]))
    project_id = project["id"]
    field_data = json.loads(run_gh([
        "project", "field-list", str(project_number), "--owner", OWNER, "--format", "json",
    ]))
    fields = {field["name"]: field for field in field_data["fields"]}
    required = {"Status", "Priority", "Area", "Release", "Work ID"}
    missing = required - fields.keys()
    if missing:
        raise RuntimeError(f"Project is missing fields: {', '.join(sorted(missing))}")

    query = """
query($project: ID!) {
  node(id: $project) {
    ... on ProjectV2 {
      items(first: 100) {
        nodes { id isArchived content { ... on Issue { title url } } }
      }
    }
  }
}
"""
    result = json.loads(run_gh([
        "api", "graphql", "-f", f"query={query}", "-f", f"project={project_id}",
    ]))
    items = result["data"]["node"]["items"]["nodes"]
    items_by_id = {
        item["content"]["title"].split(":", 1)[0]: item
        for item in items
        if item.get("content") and item["content"].get("title")
    }

    mutation = """
mutation(
  $project: ID!, $item: ID!, $workField: ID!, $work: String!,
  $priorityField: ID!, $priority: String!, $areaField: ID!, $area: String!,
  $releaseField: ID!, $release: String!, $statusField: ID!, $status: String!
) {
  work: updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $workField, value: { text: $work }
  }) { projectV2Item { id } }
  priority: updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $priorityField,
    value: { singleSelectOptionId: $priority }
  }) { projectV2Item { id } }
  area: updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $areaField,
    value: { singleSelectOptionId: $area }
  }) { projectV2Item { id } }
  release: updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $releaseField,
    value: { singleSelectOptionId: $release }
  }) { projectV2Item { id } }
  status: updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $statusField,
    value: { singleSelectOptionId: $status }
  }) { projectV2Item { id } }
}
"""
    unarchive_mutation = """
mutation($project: ID!, $item: ID!) {
  unarchiveProjectV2Item(input: { projectId: $project, itemId: $item }) {
    item { id }
  }
}
"""
    for ticket in tickets:
        item = items_by_id.get(ticket.identifier)
        if item is None:
            raise RuntimeError(f"Project item missing for {ticket.identifier}")
        if item["isArchived"]:
            run_gh([
                "api", "graphql", "-f", f"query={unarchive_mutation}",
                "-f", f"project={project_id}", "-f", f"item={item['id']}",
            ])
        status = "Done" if ticket.completed else "In Progress" if ticket.in_progress else "Todo"
        run_gh([
            "api", "graphql", "-f", f"query={mutation}",
            "-f", f"project={project_id}", "-f", f"item={item['id']}",
            "-f", f"workField={fields['Work ID']['id']}", "-f", f"work={ticket.identifier}",
            "-f", f"priorityField={fields['Priority']['id']}",
            "-f", f"priority={option_id(fields['Priority'], ticket.priority)}",
            "-f", f"areaField={fields['Area']['id']}",
            "-f", f"area={option_id(fields['Area'], AREAS[ticket.milestone])}",
            "-f", f"releaseField={fields['Release']['id']}",
            "-f", f"release={option_id(fields['Release'], ticket.milestone)}",
            "-f", f"statusField={fields['Status']['id']}",
            "-f", f"status={option_id(fields['Status'], status)}",
        ])


def option_id(field: dict[str, object], name: str) -> str:
    for option in field.get("options", []):
        if option["name"] == name:
            return str(option["id"])
    raise RuntimeError(f"Field {field['name']} has no option named {name}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="write GitHub state")
    parser.add_argument("--project-number", type=int, default=0)
    parser.add_argument("--project-fields-only", action="store_true")
    args = parser.parse_args()
    tickets = parse_roadmap(ROADMAP_PATH.read_text(encoding="utf-8"))
    print(f"Parsed {len(tickets)} canonical tickets across {len(MILESTONES)} milestones.")
    if not args.apply:
        print("Dry run only; pass --apply to synchronize GitHub.")
        return 0
    run_gh(["repo", "view", REPOSITORY, "--json", "name"])
    if args.project_fields_only:
        if args.project_number <= 0:
            raise ValueError("--project-fields-only requires --project-number")
        sync_project_fields(tickets, args.project_number)
        print(f"Synchronized project fields for {len(tickets)} items.")
        return 0
    ensure_labels()
    milestone_numbers = ensure_milestones()
    urls = sync_issues(tickets, milestone_numbers)
    if args.project_number > 0:
        add_to_project(urls, args.project_number)
        sync_project_fields(tickets, args.project_number)
    run_gh([
        "api", "--method", "PATCH",
        f"repos/{REPOSITORY}/milestones/{milestone_numbers['M0']}", "-f", "state=closed",
    ])
    print(f"Synchronized {len(urls)} issues and {len(milestone_numbers)} milestones.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
