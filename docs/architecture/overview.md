# Architecture overview

Fadrio presents logical applications while containing PipeWire's transient nodes below the user-facing model.

```text
Fadrio.UI / Fadrio.Cli
          ↓
Fadrio.Application
          ↓
      Fadrio.Core
          ↑
Infrastructure / Platform.Linux / NativeInterop
          ↓
libfadrio_native.so → PipeWire
```

Dependencies point toward Core. Platform implementations satisfy interfaces owned by Core or Application; Core never imports platform, storage, UI, Steam, or MIDI APIs.

One serialized coordinator consumes backend events, resolves identity, groups sessions by canonical `ApplicationId`, and publishes immutable `MixerSnapshot` values. Every future control source must use the same command path.

See `TECH_SPEC.md` for the authoritative complete architecture.
