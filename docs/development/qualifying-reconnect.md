# Qualifying PipeWire reconnect

`FAD-0015` combines deterministic managed tests with the isolated PipeWire daemon harness.

The managed suite verifies that a disconnect clears the visible application snapshot, reconnect creates a new backend generation, stale events from an older generation are rejected, and commands after reconnect target only the rebuilt backend. `ReconnectingAudioBackend` applies bounded exponential backoff and resets it after a ready event.

The integration harness verifies the native and managed path against a real isolated PipeWire daemon:

1. A long-running `fadrioctl apps --watch` client observes the initial registry.
2. The fixture stream and daemon are terminated.
3. A fresh daemon is started on the same isolated socket.
4. A replacement stream appears once under the rebuilt registry, with no duplicate application.
5. A new client sets the replacement stream to 45% and mutes it successfully.

Run the complete evidence with:

```bash
FADRIO_RUN_PIPEWIRE_INTEGRATION=1 ./scripts/test.sh
```

The harness owns its temporary runtime directory and terminates only the exact processes it created. Restart testing never touches the user's normal PipeWire socket or physical devices.
