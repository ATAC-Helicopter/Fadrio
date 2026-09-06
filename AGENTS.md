# Fadrio contributor rules

- Read `TECH_SPEC.md` before architectural or product changes. It is authoritative.
- Preserve the dependency direction and application-centric model in `TECH_SPEC.md`.
- `Fadrio.Core` must remain free of Avalonia, PipeWire, SQLite, Linux, Steam, and MIDI dependencies.
- Never replace native PipeWire integration with shell-command parsing.
- Keep raw node IDs and process IDs out of normal UI state.
- Add fixture tests for resolver changes.
- Treat disappearing processes, streams, and controllers as normal runtime conditions.
- Copy native callback data immediately; native callbacks must never touch Avalonia.
- Keep persistent identity stable and evidence-based. PID is diagnostic data only.
- Do not add telemetry, network listeners, root requirements, DSP, or routing features.
- Record material architectural deviations in `docs/decisions/`.
- Keep patches scoped, preserve user changes, and run `./scripts/test.sh` when native prerequisites are installed.
