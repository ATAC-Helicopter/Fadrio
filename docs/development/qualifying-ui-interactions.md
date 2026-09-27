# FAD-0503 interaction qualification

Recorded on 2026-09-27. This is an interaction slice, not completion of the production UI or `REL-00006` desktop matrix.

## Implemented behavior

Active logical applications sort audible-first, then by display name and canonical identity. Existing row objects are reused. While a volume pointer or keyboard gesture is active, collection insertions, removals and moves wait until the gesture ends. Missing targets disable immediately; their queued commands are discarded. Backend volume echoes do not move the thumb during a gesture. Persistent pinned/remembered inactive rows and their grace period belong to `FAD-0303` and are not implemented here.

Volume and mute each have at most one in-flight command and one latest pending value per row. Native round trips run off the Avalonia thread. The native lifecycle gate prevents command/dispose overlap from accessing a destroyed context. Failed commands show a generic row message and restore observed state; disconnect clears pending intent rather than replaying it after reconnect.

`BUG-00003` was found through the rendered drag: the first volume change caused a partial node-info property update to erase application identity, disabling the row. The bridge now preserves omitted metadata and honors supplied replacements/removals. A private native fixture covers delta, replacement, removal and PID parsing behavior.

## Local evidence

- `FADRIO_RUN_PIPEWIRE_INTEGRATION=1 ./scripts/test.sh`: managed tests, two native tests and isolated stream/restart integration passed.
- `dotnet test tests/Fadrio.UI.Tests --no-restore`: 16 interaction/presentation tests passed, including property notifications, sorting, row reuse, freeze, disappearance, command coalescing, disconnect discard and failure rollback.
- `dotnet format Fadrio.slnx --verify-no-changes --no-restore`, roadmap audit and whitespace check passed.
- Local ASAN/UBSAN build: both native tests and the isolated PipeWire command/churn/restart harness passed with fatal sanitizer errors enabled and mixed-runtime leak detection disabled. These are local results, not remote CI qualification.
- Actual Avalonia window on GNOME Wayland through XWayland, 360 x 680, isolated silent playback: drag reached 37%, Home plus ten Right keys reached 10%, and the mute button muted the stream. Stopping the private PipeWire daemon during another active drag caused no crash; the same controls worked again after restarting the daemon and creating a fresh stream.
- The rendered unavailable and connected-empty states were inspected, as was the recovered row. No ordinary host audio was modified.

![Recovered fixture at 10 percent, muted](../screenshots/fad-0503-recovered-fixture.png)

![Connected with no playback applications](../screenshots/fad-0503-connected-empty.png)

![Audio unavailable and reconnecting](../screenshots/fad-0503-reconnecting.png)

## Reproduce the rendered check

After a normal local build, close other Fadrio windows and run:

```bash
./scripts/test-ui-interactions.sh
```

This optional developer harness requires an X11/XWayland display, DejaVu fonts, X11/XTest libraries, `xwininfo`, `xprop`, `ffmpeg`, PipeWire tools, Python and .NET. It injects mouse/keyboard events into the exact Fadrio window whose process ownership it verifies. Do not interact with the desktop while it runs. It restores the prior pointer/focus and terminates only its owned processes. Its private runtime/data/cache directory is retained with logs and screenshots; the path is printed at exit. It is not part of headless CI or the supported-desktop matrix.

The fixture intentionally sets a process-local minimal Fontconfig configuration. It does not change host fonts or ship a restricted-font workaround in the app.

## Open qualification limits

- `BUG-00004`: normal launch with this host's full Fontconfig configuration stalled before `App.Initialize`, consuming a CPU core. The startup trace showed font/cache enumeration. A restricted process-local DejaVu configuration allowed the window to open; a software-rendering attempt did not resolve normal startup. This isolates a useful investigation path, not a proven upstream cause or a production fix. Default host-font startup is unqualified.
- The checks above are XWayland, not native Wayland, KDE, screen-reader, fractional-scaling, packaged installation or long-running qualification. Those gates remain open.
- Output control, icons, persistence of pinned/inactive rows, tray integration and localization are still later roadmap work. No new release is justified by this slice alone.
