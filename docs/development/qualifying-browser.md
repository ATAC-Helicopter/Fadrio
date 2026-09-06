# Qualifying browser identity

`FAD-0012` qualifies browser multi-stream identity. Firefox-shaped fixtures retain deterministic coverage, while the real-browser observation uses the Brave installation available on the development host.

## Automated evidence

The isolated PipeWire harness runs copied `pw-cat` clients under a `firefox` executable name and supplies the synthetic `firefox.desktop` fixture through an isolated `XDG_DATA_HOME`. This is not Firefox. It exercises the same Linux boundaries deterministically:

- playback nodes arrive through the native PipeWire bridge;
- `/proc/<pid>/exe` resolves each fixture process;
- the executable is corroborated against the XDG desktop index;
- two streams group under `xdg:firefox` with High confidence and the Firefox icon;
- volume and mute commands reach every grouped node;
- removing and recreating a stream preserves one canonical application identity.

The resolver also has a Brave fixture for the installed-package shape: the XDG launcher executes `brave-browser-stable`, the audio service runs as `/opt/brave.com/brave/brave`, and PipeWire supplies the exact `brave-browser` desktop-ID hint as its icon name.

Run the automated evidence after building with:

```bash
FADRIO_RUN_PIPEWIRE_INTEGRATION=1 ./scripts/test.sh
```

## Real Brave observation

The following observation passed on 2026-09-04:

- Zorin OS 18.1, Wayland GNOME session;
- Brave Debian package `1.94.119` (`Brave Browser 152.1.94.119`);
- PipeWire 1.0.5;
- two isolated Brave profiles playing the repository's local low-gain Web Audio fixture;
- one `Brave Web Browser` application with canonical ID `xdg:brave-browser`, High confidence, the installed `brave-browser` icon, and two sessions;
- setting volume to 35%, muting, and unmuting reached both sessions;
- terminating one profile reduced the application to one session;
- starting a replacement profile restored two sessions under the same canonical ID.

The fixture creates no network request and uses a very low-gain oscillator. Qualification output excludes runtime PIDs, profile paths, private browsing data, media titles, full command lines, and environment dumps.

## Reproduction checklist

1. Launch two isolated browser profiles against `tests/fixtures/browser/brave-audio.html` with autoplay allowed.
2. Confirm one installed browser identity, two sessions, a stable XDG canonical ID, and High confidence.
3. Set volume, mute, and unmute through `fadrioctl`; confirm every browser session follows and unrelated applications do not.
4. Stop one profile and confirm the application remains with one session.
5. Start a replacement profile and confirm it rejoins the same canonical application.
6. Record the desktop environment, distribution, browser packaging type/version, and redacted output.
