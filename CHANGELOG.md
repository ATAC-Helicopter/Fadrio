# Changelog

All notable changes will be documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and releases will use semantic versioning.

## [Unreleased]

### Added

- `FAD-0202` Secure local PNG icon resolution with trusted XDG roots, bounded caching, and traversal, symlink, malformed-file, and size protections.
- `FAD-0203` Flatpak-first canonical identity from bounded sandbox metadata and exported host desktop entries.
- `FAD-0204` Optional Snap identity from corroborated wrapper metadata without invoking or depending on Snap tooling.

### Changed

- `FAD-0205` XDG desktop-entry snapshots now refresh after debounced filesystem changes, advance a monotonic identity revision, and re-resolve active sessions once through the serialized mixer coordinator.

## [0.2.0-alpha.2] - 2026-09-13

### Changed

- `FAD-0020` Adopted Fadrio across the product, repository, managed projects, namespaces, executables, native ABI, application ID, local paths, documentation, tracker prefix, release artifacts, and website; replaced the former artwork with the supplied Fadrio branding package.

## [0.2.0-alpha.1] - 2026-09-06

### Added

- `FAD-0019` Public repository metadata, contributor setup validation, and tag-driven source-only alpha releases with SHA-256 checksums.
- `FAD-0017` Active GitHub ruleset protecting `main` through pull requests and strict Linux/native sanitizer checks.
- `REL-00002` Reproducible M0.2 qualification evidence covering real browser/Electron identity, Proton evidence, PipeWire restart recovery, and native sanitizer runs.
- `FAD-0013` Electron identity fixtures and a real VS Code terminal-bell observation resolve generic Chromium audio metadata to the installed application's name and icon.

- `FAD-0014` Initial Steam/Proton identification using bounded process evidence, cached library/manifests, conflict fixtures, and a read-only Euro Truck Simulator 2 observation.

- `FAD-0011` Hardware-free PipeWire integration coverage for application discovery, multi-session control, removal, daemon restart, and managed reconnect.
- `FAD-0012` Deterministic browser fixtures and a real Brave multi-stream qualification covering XDG identity, grouping, command fan-out, removal, and recreation.
- `FAD-0015` End-to-end PipeWire restart qualification covering unavailable state, bounded reconnect, registry replacement, stale-event rejection, and recovered commands.
- `FAD-0016` ASAN/UBSAN CI coverage for native lifecycle, callback ownership, rapid node churn, command teardown, and PipeWire reconnect.
- `FAD-0018` Theme-aware repository wordmarks and curated public presentation artwork for the FG Labs project page.

### Changed

- `FAD-0201` Application identity now scores executable and desktop metadata together, records conflicting candidates, and falls back safely instead of guessing.
- `FAD-0012` Exact PipeWire desktop-ID hints can corroborate installed applications whose launchers wrap a differently named runtime executable.

### Fixed

- `BUG-00001` Roadmap synchronization can update managed issues after their GitHub milestone is closed.
- `BUG-00002` Native volume and mute commands now complete a PipeWire round trip before short-lived clients tear down their connection.

## [0.1.0-m0] - 2026-09-04

### Added

- `FAD-0001` M0 repository scaffold, .NET 10 solution, governance, licensing, documentation, and CI.
- `FAD-0002` Application-centric domain model and stable identity contracts.
- `FAD-0003` Native PipeWire C bridge with playback discovery and volume/mute commands.
- `FAD-0004` Managed native event copying and serialized event channel.
- `FAD-0005` Linux `/proc` and cached XDG application identity resolution.
- `FAD-0006` Multi-session application aggregation and immutable mixer coordination.
- `FAD-0007` Diagnostic CLI application listing, volume, and mute commands.
- `FAD-0008` Minimal Avalonia shell and theme infrastructure.
- `FAD-0010` Managed reconnect supervision and controlled multi-session qualification.
- `REL-00001` M0 build, test, native lifecycle, and live PipeWire evidence baseline.

[0.1.0-m0]: https://github.com/ATAC-Helicopter/Fadrio/releases/tag/v0.1.0-m0
[0.2.0-alpha.1]: https://github.com/ATAC-Helicopter/Fadrio/releases/tag/v0.2.0-alpha.1
[0.2.0-alpha.2]: https://github.com/ATAC-Helicopter/Fadrio/releases/tag/v0.2.0-alpha.2
