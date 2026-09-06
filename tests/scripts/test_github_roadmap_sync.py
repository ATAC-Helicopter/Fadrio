import importlib.util
import sys
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "github_roadmap_sync.py"
SPEC = importlib.util.spec_from_file_location("github_roadmap_sync", SCRIPT)
github_sync = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = github_sync
SPEC.loader.exec_module(github_sync)


class GitHubRoadmapSyncTests(unittest.TestCase):
    def test_parser_preserves_identity_contract_and_completion(self):
        roadmap = """## M0 — Foundation
- [x] `FAD-0001` `P0` Build the foundation.
  - Scope: Keep one source of truth.
  - Acceptance: The mirror is reproducible.
## M0.2 — Qualification
- [ ] `REL-00002` `P1` Qualify the next milestone.
  - Status: In progress; evidence collection is underway.
"""
        tickets = github_sync.parse_roadmap(roadmap)
        self.assertEqual(["FAD-0001", "REL-00002"], [item.identifier for item in tickets])
        self.assertTrue(tickets[0].completed)
        self.assertEqual("M0.2", tickets[1].milestone)
        self.assertTrue(tickets[1].in_progress)
        self.assertIn("Scope: Keep one source", tickets[0].body)
        self.assertEqual("kind:rel", tickets[1].kind_label)

    def test_parser_rejects_ticket_outside_known_milestone(self):
        with self.assertRaises(ValueError):
            github_sync.parse_roadmap("## Notes\n- [ ] `FAD-0001` `P1` Invalid placement.")

    def test_option_lookup_is_exact(self):
        field = {"name": "Priority", "options": [{"id": "one", "name": "P1"}]}
        self.assertEqual("one", github_sync.option_id(field, "P1"))
        with self.assertRaises(RuntimeError):
            github_sync.option_id(field, "P0")

    def test_existing_issue_update_uses_closed_milestone_number(self):
        ticket = github_sync.Ticket(
            identifier="FAD-0001",
            title="Build the foundation.",
            priority="P0",
            milestone="M0",
            details=(),
            completed=False,
        )
        existing = {
            "FAD-0001": {
                "number": 4,
                "url": "https://github.com/example/issues/4",
                "body": f"{github_sync.MANAGED_MARKER}\n",
            }
        }
        with mock.patch.object(github_sync, "issue_index", return_value=existing), \
                mock.patch.object(github_sync, "run_gh", return_value="") as run_gh:
            urls = github_sync.sync_issues([ticket], {"M0": 7})

        self.assertEqual(["https://github.com/example/issues/4"], urls)
        patch_call = run_gh.call_args_list[0].args[0]
        self.assertEqual(["api", "--method", "PATCH"], patch_call[:3])
        self.assertIn("milestone=7", patch_call)
        self.assertNotIn("issue", patch_call[:3])


if __name__ == "__main__":
    unittest.main()
