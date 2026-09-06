# Work tracking

`ROADMAP.md` owns scope, identifiers, priorities, and completion state. GitHub mirrors execution through issues, milestones, and the **Fadrio Roadmap** project.

- Repository: <https://github.com/ATAC-Helicopter/Fadrio>
- Project: <https://github.com/users/ATAC-Helicopter/projects/9>

## Identity rules

- `FAD-xxxx`: product or engineering work.
- `BUG-xxxxx`: a defect with observed incorrect behavior.
- `REL-xxxxx`: a release or qualification gate.
- One item, one ID. IDs never change category, move to another item, or get reused.
- Titles begin with the canonical ID: `FAD-0011: Build an isolated PipeWire integration-test harness`.
- Pull requests include the owning ID in their title or body.
- Changelog entries use IDs for release-scope changes.

## Source-of-truth rule

Roadmap scope and acceptance criteria win if a GitHub description drifts. Changes are made in the roadmap first, audited, then mirrored. Implementation details may live in issues; they cannot silently expand product scope.

An incomplete ticket is Todo unless its roadmap details include `Status: In progress`; completed checkboxes always map to Done. This keeps execution state reproducible without a hard-coded active ticket in the synchronizer.

Run the local integrity check with:

```bash
python3 scripts/roadmap_audit.py
```

The check rejects duplicate IDs, malformed execution tickets, ledger collisions, and release-scope changelog bullets without IDs.

Preview the GitHub mirror parser with:

```bash
python3 scripts/github_roadmap_sync.py
```

Maintainers may apply missing labels, milestones, issues, and project membership with `--apply --project-number <number>`. The synchronizer only rewrites issue bodies carrying its ownership marker; manually maintained bodies are preserved.

Repository protections and their current enforcement state are recorded in [repository-rules.md](repository-rules.md). A hosting limitation never changes the intended collaboration policy; it remains a tracked work item until enforcement is active.
