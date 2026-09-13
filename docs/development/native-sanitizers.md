# Native sanitizer qualification

`FAD-0016` keeps memory and undefined-behavior checks in the required GitHub workflow instead of relying on an occasional local run.

The `native-sanitizers` job builds the C bridge with AddressSanitizer and UndefinedBehaviorSanitizer, then runs:

- 1,000 create/stop/destroy lifecycle iterations;
- the managed build against the instrumented bridge;
- isolated PipeWire discovery and multi-session commands;
- repeated node creation/removal while a callback consumer remains connected;
- native command teardown and daemon reconnect.

Leak detection is disabled for the mixed .NET, PipeWire, and GLib process because those runtimes retain process-lifetime allocations outside Fadrio's ownership. Invalid access, use-after-free, buffer errors, and undefined behavior remain fatal.

The normal `linux` job remains the release check. The sanitizer job is an additional M0.2 qualification gate and must also pass before merging native changes.
