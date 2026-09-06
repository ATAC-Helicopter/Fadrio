# Integration tests

Integration tests may create controlled local PipeWire streams but must not alter unrelated sessions. They need deterministic cleanup and must be opt-in when they require a user audio service.

Run the isolated PipeWire topology and control test after building:

```bash
FADRIO_RUN_PIPEWIRE_INTEGRATION=1 ./scripts/test.sh
```

The harness replaces `XDG_RUNTIME_DIR`, starts its own hardware-free PipeWire configuration, creates unlinked silent streams, verifies grouping, volume, mute, removal, recreation, and reconnect, and terminates only the exact processes it created. Its Firefox-shaped executable and desktop entry exercise the real Linux `/proc` and XDG identity path without pretending to be a real-browser qualification run.
