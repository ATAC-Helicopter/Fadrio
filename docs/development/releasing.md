# Releasing

Fadrio uses semantic versioning. Before 1.0, release channels align with roadmap maturity:

- `v0.1.0-m0` is the historical M0 engineering baseline.
- `v0.x.0-alpha.N` publishes an evaluation snapshot from active milestone `0.x`.
- `v0.9.0-beta.N` is reserved for builds that pass the public-beta gates.
- `v1.0.0` and later are stable only after every stable-release gate is evidenced.

An alpha is not a supported binary distribution. Until packaging work is complete, GitHub releases contain a reproducible source archive and its SHA-256 checksum. GitHub's automatic source links remain available as well.

## Release procedure

1. Update `Directory.Build.props`, the native CMake project version, `CHANGELOG.md`, `ROADMAP.md`, and `docs/releases/<tag>.md` in a release pull request.
2. Run `./scripts/test.sh` and the roadmap/script audits.
3. Merge only after the strict `linux` and `native-sanitizers` checks pass.
4. Create and push an annotated tag matching the managed project version, for example `v0.2.0-alpha.1`.
5. The `source-release` workflow rebuilds and tests the tag, checks version/note alignment, creates a prefixed source archive, writes its checksum, and publishes a GitHub prerelease.
6. Verify the release page, checksum, repository homepage, FG Labs project page, and roadmap/project state.

Never move or reuse a published tag. If release publication fails, fix the workflow through a pull request and rerun the failed tag workflow; do not replace already downloaded assets silently.

Before a beta or stable release, also validate supported desktop sessions, distributions, real application identities, PipeWire restart behavior, clean installation/removal, and documentation accuracy. Binary package smoke tests become mandatory once binary packaging exists.
