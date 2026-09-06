# ADR-0002: Adopt Fadrio as the complete product identity

- Status: Accepted
- Date: 2026-09-13
- Owner: `FAD-0020`

## Context

The final Fadrio brand was selected while the application was still a source-only alpha, before persistence, packaged desktop integration, D-Bus consumers, or a stable native ABI created a compatibility obligation.

## Decision

Use Fadrio consistently for the product, repository, managed projects and namespaces, executable names, native library and exported C symbols, reverse-DNS application identity, XDG paths, release archives, work-item prefix, and website route. The supplied Fadrio raster package replaces the former branding assets.

The new identifiers are `fadrio`, `fadrioctl`, `libfadrio_native.so`, `dev.fglabs.Fadrio`, and `FAD-xxxx`. Existing GitHub issue numbers and Git tags remain stable so historical links continue to resolve.

## Consequences

The rename intentionally breaks compatibility with the retired development identifiers. No migration is required because affected persistence and public IPC contracts have not shipped. Future compatibility-sensitive identifiers must follow the stability rules in `TECH_SPEC.md`.
