<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/branding/raster/fadrio-banner-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="assets/branding/raster/fadrio-banner-light.png">
    <img src="assets/branding/raster/fadrio-banner-light.png" width="740" alt="Fadrio — your apps, your sound; a simple application mixer for Linux">
  </picture>
</p>

<p align="center">
  A Linux volume mixer that controls applications—not raw audio streams.
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: GPL-3.0-or-later" src="https://img.shields.io/badge/license-GPL--3.0--or--later-6f42c1.svg"></a>
  <a href="https://github.com/ATAC-Helicopter/Fadrio/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/ATAC-Helicopter/Fadrio?include_prereleases&sort=semver"></a>
  <img alt="Platform: Linux" src="https://img.shields.io/badge/platform-Linux-fcc624.svg">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512bd4.svg">
  <img alt="Status: public alpha" src="https://img.shields.io/badge/status-public%20alpha-orange.svg">
</p>

Fadrio resolves PipeWire playback nodes into stable logical applications and groups every session owned by an application behind one mixer target. Three browser streams should look like one **Brave** or **Firefox** control—not three nodes, renderer processes, or PIDs.

> [!IMPORTANT]
> Fadrio is a source-only public alpha, not an end-user release. No supported binary package is published yet. Packaging, persistence, MIDI control, and the finished interface are not implemented. Initial Steam/Proton identification requires corroborating local process, compatibility-directory, and installed-manifest evidence.

## What works today

- Native PipeWire registry connection and playback-node events.
- Versioned C ABI with copied, serialized managed events.
- Linux `/proc` and cached XDG desktop-entry identity resolution.
- Deterministic multi-evidence scoring with safe conflict fallback.
- Stable fallback identities that never use PID.
- Multi-session application grouping and application-wide volume/mute commands.
- Managed reconnect supervision with generation isolation.
- Diagnostic CLI and minimal Avalonia shell.

## Architecture

```text
PipeWire sessions
    ↓
Application identity resolver
    ↓
Logical applications
    ↓
Mixer state
    ↓
UI / MIDI / CLI / D-Bus
```

The complete product contract is in [TECH_SPEC.md](TECH_SPEC.md). Start with the shorter [architecture overview](docs/architecture/overview.md) when contributing.

Planning is tracked in the canonical [ROADMAP.md](ROADMAP.md) and mirrored to the [Fadrio Roadmap GitHub Project](https://github.com/users/ATAC-Helicopter/projects/9). Alpha source snapshots and their checksums are published on [GitHub Releases](https://github.com/ATAC-Helicopter/Fadrio/releases).

## Build

Install the dependencies in [the Linux setup guide](docs/development/setup-linux.md), then run:

```bash
./scripts/build.sh
./scripts/test.sh
```

Inspect current logical applications:

```bash
dotnet run --project src/Fadrio.Cli -- apps --watch
```

Exercise the shared M0 command path:

```bash
dotnet run --project src/Fadrio.Cli -- set <canonical-id> 35
dotnet run --project src/Fadrio.Cli -- mute <canonical-id>
dotnet run --project src/Fadrio.Cli -- unmute <canonical-id>
```

These commands currently open a short-lived PipeWire client. A later milestone will route them through the single running instance over D-Bus.

## Scope

Fadrio is not a PipeWire graph editor, DAW, DSP host, recorder, virtual-device manager, or network audio server. See [product scope](docs/product/scope.md).

## Contributing and security

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md), the repository-specific [agent rules](AGENTS.md), and the [Code of Conduct](CODE_OF_CONDUCT.md). Report vulnerabilities using [SECURITY.md](SECURITY.md), not a public issue.

## License

Copyright © 2026 Fadrio contributors. Fadrio is free software licensed under [GPL-3.0-or-later](LICENSE). Third-party components retain their respective licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
