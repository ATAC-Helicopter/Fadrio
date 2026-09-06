# Audio backend

The production backend is the native PipeWire API. Shell tools are permitted only for developer comparison and fixture qualification.

`libfadrio_native.so` owns PipeWire contexts, loops, registries, proxies, and listeners. Its C ABI exposes opaque handles and normalized events. Callback structures and UTF-8 strings are borrowed for callback duration only; managed code copies them immediately.

Playback nodes are currently identified by `media.class=Stream/Output/Audio`. Properties are optional. Volume is normalized to `0..1`, and application commands fan out the same absolute value or mute state to all owned sessions.

Managed reconnect supervision assigns a new generation to each connection so stale events cannot affect rebuilt state.
