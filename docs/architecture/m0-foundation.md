# M0 technical foundation

The implementation preserves this one-way flow:

```text
PipeWire playback nodes
  -> versioned C ABI callbacks
  -> copied managed backend events
  -> serialized mixer state coordinator
  -> evidence-based application resolver
  -> immutable logical-application snapshots
  -> CLI or UI presentation
```

Native callback values are borrowed only during the callback. `Fadrio.NativeInterop` copies all strings immediately and places normalized events on a single-reader channel. Neither native code nor callbacks know about Avalonia.

The XDG desktop index is constructed once. Resolver requests query that cache rather than rescanning application directories. `/proc` disappearance and inaccessible process metadata are normal outcomes; identities degrade to PipeWire evidence or a stable fallback hash. PID is retained only as runtime diagnostic data.

Steam/Proton resolution is represented by `ISteamApplicationResolver` but intentionally has no production implementation in M0.

`ReconnectingAudioBackend` recreates a failed backend with bounded exponential backoff. It remaps each connection to a monotonically increasing generation so delayed events from an earlier PipeWire connection cannot mutate rebuilt state.

## M0.1 live qualification

On 2026-09-04, two silent `pw-cat` playback streams with the same application metadata were observed as one logical application with two sessions. The diagnostic CLI set both sessions from 80% to 35%, muted and unmuted both, and left an unrelated playback application unchanged. This qualifies the native registry, managed callback, resolver, aggregation, command fan-out, volume, and mute path on the development host. It is not a substitute for the broader Firefox/Discord/Proton manual matrix.
