import importlib.util
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "roadmap_audit.py"
SPEC = importlib.util.spec_from_file_location("roadmap_audit", SCRIPT)
roadmap_audit = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = roadmap_audit
SPEC.loader.exec_module(roadmap_audit)


class RoadmapAuditTests(unittest.TestCase):
    def test_accepts_unique_canonical_tickets_and_unallocated_ledger(self):
        roadmap = """- [ ] `FAD-0001` `P1` Build one thing.
- [x] `REL-00001` `P0` Qualify it.
- Next `FAD-00xx`: `FAD-0002`
- Next `REL`: `REL-00002`
"""
        changelog = "- `FAD-0001` Added one thing."
        self.assertEqual([], roadmap_audit.audit(roadmap, changelog))

    def test_rejects_duplicates_malformed_tickets_and_missing_changelog_ids(self):
        roadmap = """- [ ] `FAD-0001` `P1` Build one thing.
- [ ] `FAD-0001` Missing priority.
- Next `FAD-00xx`: `FAD-0001`
"""
        errors = roadmap_audit.audit(roadmap, "- Added one thing.")
        self.assertTrue(any("malformed" in error for error in errors))
        self.assertTrue(any("already allocated" in error for error in errors))
        self.assertTrue(any("no work ID" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
