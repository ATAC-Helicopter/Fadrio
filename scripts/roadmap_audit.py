#!/usr/bin/env python3
"""Validate Fadrio's canonical work-item identifiers and roadmap ledger."""

from __future__ import annotations

import re
import sys
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ROADMAP = ROOT / "ROADMAP.md"
CHANGELOG = ROOT / "CHANGELOG.md"
ID_PATTERN = re.compile(r"(?:FAD-\d{4}|BUG-\d{5}|REL-\d{5})")
TICKET_PATTERN = re.compile(
    r"^- \[(?P<done>[ xX])\] `(?P<id>FAD-\d{4}|BUG-\d{5}|REL-\d{5})` "
    r"`(?P<priority>P[012])` (?P<title>\S.*)$"
)
LEDGER_PATTERN = re.compile(
    r"^- Next `(?P<family>FAD-[0-9x]+|BUG|REL)`: `(?P<id>FAD-\d{4}|BUG-\d{5}|REL-\d{5})`$"
)


def audit(roadmap_text: str, changelog_text: str) -> list[str]:
    errors: list[str] = []
    tickets: list[str] = []
    ledger: list[str] = []

    in_fence = False
    for number, line in enumerate(roadmap_text.splitlines(), start=1):
        if line.startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        if line.startswith("- ["):
            match = TICKET_PATTERN.fullmatch(line)
            if match is None:
                errors.append(f"ROADMAP.md:{number}: malformed execution ticket")
            else:
                tickets.append(match.group("id"))
        ledger_match = LEDGER_PATTERN.fullmatch(line)
        if ledger_match is not None:
            ledger.append(ledger_match.group("id"))

    for identifier, count in Counter(tickets).items():
        if count > 1:
            errors.append(f"ROADMAP.md: duplicate ticket ID {identifier}")

    if not tickets:
        errors.append("ROADMAP.md: no execution tickets found")
    if not ledger:
        errors.append("ROADMAP.md: identifier ledger is missing")

    allocated = set(tickets)
    for identifier in ledger:
        if identifier in allocated:
            errors.append(f"ROADMAP.md: ledger next ID is already allocated: {identifier}")

    for number, line in enumerate(changelog_text.splitlines(), start=1):
        if line.startswith("- ") and "M0 repository" not in line and ID_PATTERN.search(line) is None:
            errors.append(f"CHANGELOG.md:{number}: release-scope bullet has no work ID")

    return errors


def main() -> int:
    errors = audit(
        ROADMAP.read_text(encoding="utf-8"),
        CHANGELOG.read_text(encoding="utf-8"),
    )
    if errors:
        print("Roadmap audit failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print("Roadmap audit passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
