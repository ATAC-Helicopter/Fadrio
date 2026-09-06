# Testing

Run all managed and native tests with:

```bash
./scripts/test.sh
```

Unit tests must not require audio hardware, a real `/proc`, Steam, or a controller. Use fixtures and fake backends. Real PipeWire qualification is a separate integration/manual layer and must use controlled silent streams where possible.

Resolver coverage should include missing/disappearing processes, ambiguous desktop entries, native applications, browser identity, executable fallback, and eventually Proton. Native coverage should focus on lifecycle, event translation, ownership, commands, and reconnect behavior.

The opt-in isolated PipeWire test is enabled with `FADRIO_RUN_PIPEWIRE_INTEGRATION=1 ./scripts/test.sh`. It uses a private runtime directory and unlinked silent streams, leaving the user's normal PipeWire instance and applications untouched.

Browser qualification evidence and its reproducible checklist are documented in [qualifying-browser.md](qualifying-browser.md).
PipeWire restart and command-recovery evidence is documented in [qualifying-reconnect.md](qualifying-reconnect.md).
Native ownership and sanitizer coverage is documented in [native-sanitizers.md](native-sanitizers.md).
