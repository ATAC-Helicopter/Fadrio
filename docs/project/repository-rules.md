# Repository rules

`FAD-0017` owns GitHub-side enforcement of this policy. These rules follow the same protected-history intent as VaultSync while matching Fadrio's current Linux-only CI and single `main` release spine.

## Intended `main` ruleset

- Prevent branch deletion.
- Prevent non-fast-forward updates and force-pushes.
- Require changes to arrive through a pull request.
- Dismiss stale approvals when a pull request changes.
- Require all review conversations to be resolved.
- Require the `linux` status check from `build-and-test` against the latest `main` before merge.
- Require the `native-sanitizers` status check added during M0.2 qualification.
- Allow merge commits so the owning work ID and pull-request boundary remain visible in history.
- Permit repository administrators to bypass only for recovery or an explicitly documented emergency.

Pull-request review count and CODEOWNERS approval become required when a second maintainer can provide independent review. Until then, CI and the pull-request record are mandatory even when the repository owner performs the merge.

## Current enforcement

As of 2026-09-06, Fadrio is public and GitHub ruleset `22393100` actively protects `main`. GitHub reports the branch as protected. Pull requests, resolved review conversations, and strict successful `linux` and `native-sanitizers` checks are required; deletion and non-fast-forward updates are blocked. Administrators retain the documented emergency/recovery bypass.

The reviewable API payload is [main-ruleset.json](main-ruleset.json). After intentional policy changes, update that file first, apply it through the GitHub rules API, and read the active ruleset back to verify the target, bypass policy, merge method, pull-request requirements, and strict status checks.
