# Fadrio — Complete Product, UX, Technical Architecture, Repository, and Delivery Specification

> **Status:** Active architecture specification
> **Product name:** Fadrio
> **Primary platform:** Linux desktop  
> **Primary audio stack:** PipeWire  
> **Primary UI concept:** Vertical, application-centric volume mixer  
> **Primary differentiator:** Users control real applications, not raw PipeWire/PulseAudio processes or transient streams.  
> **Secondary differentiator:** External physical mixer/controller support is a first-class feature, not an add-on.  
> **Recommended implementation:** .NET 10 + Avalonia 12.x for the application layer, with a small native Linux bridge written in C for PipeWire/ALSA integration.  
> **License recommendation:** GPL-3.0-or-later for the application, unless a more permissive ecosystem strategy is preferred before public release.

---

# 0. Executive summary

Fadrio is a Linux-first desktop application whose entire purpose is to make per-application audio control simple, attractive, stable, and physically controllable.

It is **not** a PipeWire graph editor, audio workstation, virtual cable system, DAW, voice changer, or replacement for qpwgraph, Helvum, Carla, EasyEffects, or professional routing tools.

The user-facing abstraction is:

```text
Firefox
Spotify
Discord
Le Mans Ultimate
System Sounds
```

not:

```text
node 81
Firefox AudioStream
Chromium renderer
wine64-preloader
alsa_output.pci-0000_01_00.1...
```

The backend may manage many PipeWire nodes, processes, streams, channels, devices, and metadata objects, but the primary UI should represent **stable logical applications**.

The core architectural rule is:

> **One logical application identity may own zero, one, or many runtime audio streams.**

The second architectural rule is:

> **Every control source talks to the same mixer state model.**

A mouse slider, MIDI fader, HID controller, CLI command, D-Bus call, keyboard shortcut, or future Stream Deck integration must all resolve to the same internal target and use the same volume-setting path.

This prevents the product from becoming a collection of independent integrations.

---

# 1. Product definition

## 1.1 One-sentence description

**Fadrio is a polished Linux application mixer that identifies real applications, groups their audio streams, remembers user preferences, and optionally maps them to physical faders, knobs, and buttons.**

## 1.2 User problem

Existing Linux audio tools usually fall into one of four categories:

1. desktop volume settings that expose only basic output/input controls;
2. traditional PulseAudio-style mixers that expose playback streams but feel technical;
3. PipeWire graph/routing utilities that are powerful but far beyond what a normal user needs;
4. specialist audio-processing applications focused on EQ, filters, routing, or studio workflows.

The missing experience is a desktop utility where the user can immediately answer:

- What application is making sound?
- How loud is it?
- Can I mute it?
- Can I keep its level remembered?
- Can I assign this application or category to a real physical fader?

## 1.3 Target users

Primary:

- Linux gamers;
- desktop Linux users who run several audio-producing applications;
- users moving from Windows who expect an application mixer;
- users who want physical volume controls without building a studio stack;
- users with MIDI controllers, knob boxes, macro pads, or DIY USB controllers.

Secondary:

- streamers who want basic level control but do not need production routing;
- accessibility-oriented users who benefit from large physical controls;
- multi-monitor / fullscreen users who want volume control without opening system settings.

Not target users:

- audio engineers needing arbitrary routing;
- DAW users needing insert chains;
- users needing virtual microphones;
- users needing LV2/VST hosting;
- users needing complex channel matrices.

---

# 2. Product principles

These principles should be treated as design constraints.

## 2.1 Applications, not processes

The UI should display a stable user-understandable application identity whenever technically possible.

Bad:

```text
wine64-preloader
Firefox AudioStream #4
WEBRTC VoiceEngine
Chromium
```

Good:

```text
Le Mans Ultimate
Firefox
Discord
Spotify
```

## 2.2 Simple by default, inspectable when needed

The default row represents the application.

Advanced stream details may be available by expanding the row, but raw streams must never dominate the interface.

## 2.3 Stable controls

An application disappearing for a moment must not destroy the user's configuration.

Pinned applications and remembered identities persist even when no stream exists.

## 2.4 Hardware is first-class

External controllers are part of the architecture from day one.

The internal model must never assume that the GUI is the only source of volume changes.

## 2.5 Linux-native behavior

Fadrio should respect:

- PipeWire;
- XDG desktop files;
- XDG configuration/data/cache locations;
- Flatpak conventions;
- systemd user services where appropriate;
- D-Bus;
- Wayland/XWayland realities;
- Steam/Proton/Wine application behavior.

## 2.6 Do not become Voicemeeter

Feature requests must be evaluated against product scope.

The phrase "it would be cool if" is not sufficient reason to introduce routing graphs, DSP, virtual devices, or production functionality.

## 2.7 No invisible magic that cannot be explained

When Fadrio groups streams, resolves an executable, restores a saved volume, changes a profile, or maps a controller, the user should be able to inspect why.

Advanced diagnostics may show:

```text
Resolved application:
Le Mans Ultimate

Evidence:
Steam App ID: ...
Executable: ...
Desktop mapping: ...
PipeWire application.process.id: ...
```

The normal interface should remain simple.

---

# 3. Explicit non-goals

The following are **out of scope for the product core** unless a future major-version strategy intentionally changes direction:

- arbitrary PipeWire graph editing;
- virtual sinks/sources;
- virtual microphones;
- EQ;
- compressors;
- limiters;
- noise suppression;
- echo cancellation configuration;
- LV2/VST plugin hosting;
- DAW features;
- audio recording;
- streaming/broadcasting;
- per-channel surround editing;
- patch bays;
- JACK session management;
- professional mixing-console emulation;
- audio-device driver configuration;
- Bluetooth codec management;
- replacing PipeWire/WirePlumber.

Fadrio may expose a link such as **Open advanced audio routing tool** but should not implement these functions itself.

---

# 4. Naming and identity

## 4.1 Product name

`Fadrio` is the final public product name. New user-facing, package, repository, protocol, and source identifiers use Fadrio consistently.

Before broad binary distribution, verify:

- GitHub namespace availability;
- Flathub application ID availability;
- domain availability if desired;
- Debian/Fedora package-name conflicts;
- existing applications or trademarks named Fadrio;
- reverse-DNS application ID.

The previous development name has no compatibility contract. The rename is intentionally completed before persistence, D-Bus, packaged desktop entries, or a stable native ABI ship.

## 4.2 Recommended application ID

Application ID:

```text
dev.fglabs.Fadrio
```

Keep casing and naming stable after public release because desktop entries, configuration paths, Flatpak permissions, and stored identities may depend on it.

## 4.3 Executable names

GUI:

```text
fadrio
```

Optional CLI:

```text
fadrioctl
```

Optional native helper library:

```text
libfadrio_native.so
```

Do not expose multiple vaguely named executables to users.

---

# 5. Recommended technology stack

## 5.1 Application language

### Decision: C# / .NET 10

Use .NET 10 for new development.

Why:

- current long-support generation appropriate for a new 2026 application;
- excellent async/event/state tooling;
- strong test ecosystem;
- user already has substantial Avalonia/.NET experience;
- fast UI iteration;
- easy immutable records, collections, logging, DI, configuration, SQLite, JSON;
- lower product-development cost than moving the entire project to Rust solely for Linux-native APIs.

Do not use .NET 8 for a brand-new long-lived branch unless packaging constraints require it.

Do not use Electron.

Reasons not to use Electron:

- memory overhead is unjustified for a small mixer;
- slower startup;
- poor fit for a persistent tray utility;
- extra complexity for native PipeWire integration;
- application should feel like a desktop utility, not a web shell.

## 5.2 UI framework

### Decision: Avalonia 12.x

Use a pinned stable Avalonia 12.x release.

Important platform policy:

- treat X11/XWayland as the production baseline until Avalonia's native Wayland backend is sufficiently stable for the required feature set;
- test every release under GNOME Wayland;
- evaluate native Wayland experimentally behind a build/runtime flag;
- do not make an experimental backend mandatory for v1.

Why Avalonia:

- strong custom layout/theming;
- Linux support;
- tray/window primitives;
- user familiarity;
- no need to create a separate GTK/C# binding strategy;
- MVVM-friendly;
- easy animated compact vertical UI.

Why not GTK4/Libadwaita as primary implementation:

- it would provide excellent GNOME integration but imposes a new application stack and a stronger GNOME-specific visual identity;
- the product is intended to have its own mixer aesthetic rather than being a Settings clone;
- Avalonia keeps future portability possible without making cross-platform support a v1 commitment.

GTK remains a valid fallback if Avalonia tray/Wayland behavior becomes a hard blocker.

## 5.3 Audio integration

### Decision: Native PipeWire backend

The shipping backend must use PipeWire's native API rather than treating `pactl` as the product API.

Use:

```text
libpipewire-0.3
libspa-0.2
```

and relevant SPA interfaces.

The app should subscribe to object creation/removal/change events and maintain an in-memory model.

### Development-only adapter

A `wpctl` or `pactl` adapter is acceptable for:

- UI prototyping;
- fixture generation;
- early development;
- troubleshooting.

It must not become the long-term production backend.

## 5.4 Native bridge

### Decision: small C shared library + stable C ABI

Rather than attempting broad direct P/Invoke against the entire PipeWire API, build a small native bridge:

```text
src/native/fadrio-native/
```

Responsibilities:

- initialize PipeWire;
- connect to the user's PipeWire remote;
- enumerate relevant objects;
- subscribe to registry events;
- expose normalized nodes/streams/devices;
- change mute/volume;
- subscribe to volume/property changes;
- provide peak/activity data if enabled;
- expose MIDI/ALSA functionality if kept in the same native layer.

Expose a narrow ABI such as:

```c
vm_context_create
vm_context_destroy
vm_start
vm_stop
vm_set_stream_volume
vm_set_stream_mute
vm_move_stream
vm_set_device_volume
vm_subscribe_events
```

Use opaque handles.

Do not expose PipeWire structs directly across the ABI.

Reasons:

- PipeWire is C-native;
- stable ownership can be contained in native code;
- reduces C# unsafe/PInvoke complexity;
- makes debugging lifecycle/threading issues easier;
- C ABI can later be consumed by a Rust or other frontend if required.

## 5.5 MIDI

### Decision: ALSA Sequencer through the native layer for Linux v1

For a Linux-first product, use ALSA Sequencer directly or a small well-maintained native abstraction.

Architecture must still call this feature `IMidiBackend` so another backend can be added later.

Do not make JACK mandatory.

Do not require PipeWire MIDI objects to be visible for controller support; hardware controller input should remain reliable on normal Linux desktops.

MIDI requirements:

- enumerate devices/ports;
- hotplug awareness;
- open input;
- optional output for LED/motor feedback;
- raw MIDI CC;
- note on/off;
- pitch bend if a controller uses it;
- 14-bit CC support later;
- NRPN later;
- device identity matching;
- MIDI Learn.

## 5.6 Generic HID

Not required for 1.0.

Design interface now:

```csharp
IControllerBackend
IControllerDevice
IControllerInput
IControllerFeedback
```

Implement MIDI first.

Future native backends may use:

- hidraw;
- hidapi;
- libusb;
- vendor-specific SDKs.

## 5.7 Persistence

### Decision: SQLite

Use SQLite for durable state.

Recommended library:

```text
Microsoft.Data.Sqlite
```

Data includes:

- application identities;
- identity evidence;
- aliases;
- pinned/hidden state;
- volume policy;
- group membership;
- profiles;
- controller devices;
- mappings;
- UI preferences;
- migration history;
- diagnostic-safe event metadata if required.

Do not use SQLite for high-frequency live meter samples.

## 5.8 Configuration

Use:

- SQLite for structured durable user state;
- a small JSON settings file for bootstrap-level options if needed;
- XDG-standard paths.

Recommended paths:

```text
$XDG_CONFIG_HOME/fadrio/
$XDG_DATA_HOME/fadrio/
$XDG_CACHE_HOME/fadrio/
$XDG_STATE_HOME/fadrio/
```

Fallback to the appropriate `~/.config`, `~/.local/share`, `~/.cache`, `~/.local/state`.

## 5.9 Logging

Use `Microsoft.Extensions.Logging`.

Development:

- console;
- rolling local text file.

Release:

- structured local file;
- bounded retention;
- no automatic telemetry by default.

Log paths:

```text
$XDG_STATE_HOME/fadrio/logs/
```

Default level:

```text
Information
```

Detailed PipeWire identity dumps only under:

```text
Debug
```

Never log full environment variables by default.

---

# 6. High-level architecture

```text
┌────────────────────────────────────────────────────────────┐
│                        Fadrio.UI                          │
│ Avalonia views, shell, tray popup, settings, controller UI│
└───────────────────────────┬────────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────────┐
│                    Fadrio.Application                    │
│ use cases, commands, orchestration, application services  │
└───────────────┬───────────────────────┬────────────────────┘
                │                       │
┌───────────────▼────────────┐ ┌────────▼────────────────────┐
│       Fadrio.Core         │ │   Fadrio.Infrastructure   │
│ domain objects/interfaces  │ │ SQLite, XDG, desktop files │
└───────────────┬────────────┘ └────────┬────────────────────┘
                │                       │
                │              ┌────────▼────────────────────┐
                │              │ Fadrio.Platform.Linux     │
                │              │ /proc, Steam, Flatpak,     │
                │              │ Wine/Proton resolution     │
                │              └────────┬────────────────────┘
                │                       │
                └──────────────┬────────┘
                               │
                    ┌──────────▼─────────────┐
                    │ Fadrio.NativeInterop │
                    │ C ABI wrapper         │
                    └──────────┬─────────────┘
                               │
                 ┌─────────────▼───────────────┐
                 │ libfadrio_native.so        │
                 │ PipeWire + ALSA Sequencer   │
                 └──────┬──────────────┬───────┘
                        │              │
                    PipeWire       MIDI hardware
```

---

# 7. Solution/project layout

Recommended repository:

```text
fadrio/
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug.yml
│   │   ├── feature.yml
│   │   └── hardware-support.yml
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── dependabot.yml
│   └── workflows/
│       ├── build.yml
│       ├── test.yml
│       ├── native.yml
│       ├── package.yml
│       └── release.yml
│
├── assets/
│   ├── branding/
│   │   ├── source/
│   │   ├── exports/
│   │   └── README.md
│   ├── screenshots/
│   ├── controller-diagrams/
│   ├── mockups/
│   └── store/
│
├── docs/
│   ├── architecture/
│   │   ├── overview.md
│   │   ├── audio-backend.md
│   │   ├── application-identity.md
│   │   ├── controller-system.md
│   │   ├── persistence.md
│   │   └── threading.md
│   ├── design/
│   │   ├── ui-system.md
│   │   ├── accessibility.md
│   │   ├── icons.md
│   │   └── screenshots.md
│   ├── development/
│   │   ├── setup-linux.md
│   │   ├── debugging-pipewire.md
│   │   ├── testing.md
│   │   ├── packaging.md
│   │   └── releasing.md
│   ├── decisions/
│   │   └── ADR-0001-template.md
│   └── product/
│       ├── roadmap.md
│       ├── scope.md
│       └── terminology.md
│
├── packaging/
│   ├── flatpak/
│   ├── deb/
│   ├── rpm/
│   ├── appimage/
│   └── tarball/
│
├── scripts/
│   ├── bootstrap-linux.sh
│   ├── build-native.sh
│   ├── build.sh
│   ├── test.sh
│   ├── package.sh
│   └── generate-icons.sh
│
├── src/
│   ├── Fadrio.Core/
│   ├── Fadrio.Application/
│   ├── Fadrio.Infrastructure/
│   ├── Fadrio.Platform.Linux/
│   ├── Fadrio.NativeInterop/
│   ├── Fadrio.UI/
│   ├── Fadrio.Cli/
│   └── native/
│       └── fadrio-native/
│           ├── include/
│           ├── src/
│           ├── tests/
│           └── CMakeLists.txt
│
├── tests/
│   ├── Fadrio.Core.Tests/
│   ├── Fadrio.Application.Tests/
│   ├── Fadrio.Infrastructure.Tests/
│   ├── Fadrio.Platform.Linux.Tests/
│   ├── Fadrio.NativeInterop.Tests/
│   ├── Fadrio.UI.Tests/
│   ├── fixtures/
│   │   ├── pipewire/
│   │   ├── desktop-files/
│   │   ├── proc/
│   │   ├── steam/
│   │   └── midi/
│   └── integration/
│
├── tools/
│   └── PipeWireFixtureRecorder/
│
├── .editorconfig
├── .gitattributes
├── .gitignore
├── AGENTS.md
├── CODE_OF_CONDUCT.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── LICENSE
├── README.md
├── SECURITY.md
├── TECH_SPEC.md
└── Fadrio.slnx
```

---

# 8. Dependency direction

Strict dependency rules:

```text
Core
↑
Application
↑
Infrastructure / Platform implementations
↑
UI / CLI
```

`Fadrio.Core` must not reference:

- Avalonia;
- SQLite;
- PipeWire;
- Linux `/proc`;
- Steam;
- MIDI libraries.

`Fadrio.Application` may reference `Core`, but should not depend on Avalonia.

`Fadrio.UI` may depend on `Application`, `Core`, and presentation helpers.

The native project must not depend on UI code.

No circular project references.

---

# 9. Core domain model

## 9.1 ApplicationIdentity

```csharp
public sealed record ApplicationIdentity
{
    public required ApplicationId Id { get; init; }
    public required string DisplayName { get; init; }

    public string? DesktopFileId { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ExecutableName { get; init; }
    public string? FlatpakId { get; init; }
    public string? SnapId { get; init; }
    public string? SteamAppId { get; init; }
    public string? WineExecutable { get; init; }

    public IconReference? Icon { get; init; }

    public IdentityConfidence Confidence { get; init; }
    public IReadOnlyList<IdentityEvidence> Evidence { get; init; }
}
```

## 9.2 RuntimeApplication

```csharp
public sealed class RuntimeApplication
{
    public ApplicationIdentity Identity { get; }
    public IReadOnlyList<AudioSession> Sessions { get; }

    public bool IsRunning { get; }
    public bool IsAudible { get; }
    public float EffectiveVolume { get; }
    public bool IsMuted { get; }
}
```

## 9.3 AudioSession

Represents a runtime PipeWire playback stream/node.

```csharp
public sealed record AudioSession
{
    public required AudioSessionId Id { get; init; }
    public required uint PipeWireNodeId { get; init; }

    public int? ProcessId { get; init; }

    public string? ApplicationName { get; init; }
    public string? ApplicationId { get; init; }
    public string? ProcessBinary { get; init; }
    public string? MediaName { get; init; }
    public string? MediaRole { get; init; }

    public float Volume { get; init; }
    public bool Muted { get; init; }
    public bool Active { get; init; }

    public DeviceId? OutputDevice { get; init; }
}
```

## 9.4 ApplicationProfile

Durable user preferences.

```text
ApplicationProfile
- ApplicationId
- CustomDisplayName
- CustomIcon
- Pinned
- Hidden
- RememberVolume
- VolumeRestoreMode
- LastVolume
- FixedDefaultVolume
- PreferredOutputDevice
- SortOrder
- AccentOverride
```

## 9.5 MixerTarget

Every control must eventually point to one of these:

```csharp
public abstract record MixerTarget;

public sealed record ApplicationTarget(ApplicationId Id) : MixerTarget;
public sealed record GroupTarget(GroupId Id) : MixerTarget;
public sealed record MasterTarget(DeviceId? Device) : MixerTarget;
public sealed record InputDeviceTarget(DeviceId Id) : MixerTarget;
public sealed record DynamicRoleTarget(DynamicRole Role) : MixerTarget;
```

---

# 10. PipeWire object strategy

Fadrio should connect as a normal PipeWire client and maintain a registry of relevant objects.

Track at minimum:

- clients;
- nodes;
- devices;
- ports when required;
- metadata needed for default sink and routing context.

PipeWire exposes application metadata such as logical application ID/name, icon name, process ID, and process binary. These are useful evidence, not an absolute truth.

Do not assume every application sets every property.

The resolver must tolerate:

- missing `application.id`;
- incorrect `application.name`;
- shared browser/media subprocesses;
- Wine processes;
- sandboxed apps;
- transient process IDs;
- streams that exist before `/proc` can be read;
- streams that disappear during resolution.

---

# 11. Volume semantics

## 11.1 Internal normalized value

Represent UI volume as:

```text
0.0 .. 1.0
```

for standard range.

Support >1.0 only when amplification is explicitly enabled.

Avoid using integer percentages inside core calculations.

## 11.2 Application volume

An application may own multiple sessions.

Default behavior:

```text
Set application to 0.42
→ apply 0.42 to each owned playback session
```

This produces predictable visible behavior.

Do not average session volumes and then apply a relative delta unless the user explicitly selects an advanced proportional mode.

## 11.3 External changes

If another program changes a stream's volume:

- receive backend event;
- update session;
- recompute application effective state;
- update GUI/controller feedback;
- do **not** immediately fight the external change unless the user has explicitly enabled a fixed enforced volume policy.

`Remember last volume` is not the same as `enforce volume continuously`.

## 11.4 Mute

Application mute means:

```text
mute every current session owned by that application
```

When a new session appears while the application is in a user-muted state, apply mute to it.

Persist a user-intent mute only if a future product decision explicitly defines persistent mute behavior.

---

# 12. Application identity resolver

This is the most important subsystem after the audio backend.

## 12.1 Resolver contract

Input:

```text
PipeWire session metadata
process ID if available
current process tree
desktop entry index
Steam metadata
sandbox metadata
existing user mappings
```

Output:

```text
ApplicationIdentity
confidence
evidence list
```

## 12.2 Evidence precedence

Recommended order:

1. explicit user mapping;
2. trusted Flatpak application ID;
3. desktop application ID with executable corroboration;
4. Steam App ID resolved from process environment/compat path;
5. Wine/Proton target executable + Steam metadata;
6. executable canonical path;
7. PipeWire `application.id`;
8. process binary;
9. PipeWire application name;
10. fallback unknown-session identity.

This is not merely a fixed `if/else` chain. Multiple evidence sources may strengthen one result.

## 12.3 Stable canonical IDs

Examples:

```text
xdg:org.mozilla.firefox
flatpak:com.spotify.Client
steam:1066890
exe:/usr/bin/vlc
wine:/path/to/prefix|LeMansUltimate.exe
unknown:<stable-hash-of-best-available-evidence>
```

Never use PID as a persistent ID.

## 12.4 Resolver confidence

```text
Exact
High
Medium
Low
Unknown
```

UI normally hides this.

Diagnostics may expose it.

---

# 13. Native Linux executable resolution

For a PipeWire stream with PID:

1. read `/proc/<pid>/exe`;
2. resolve symlink;
3. read `/proc/<pid>/cmdline`;
4. selectively inspect `/proc/<pid>/environ`;
5. inspect parent process chain where needed;
6. compare executable against indexed `.desktop` files.

Security rule:

Only inspect the current user's process data that the application can normally access.

Do not request root.

Do not ship setuid helpers.

---

# 14. XDG desktop application index

Index:

```text
$XDG_DATA_HOME/applications
$XDG_DATA_DIRS/*/applications
```

Parse:

- `Name`;
- localized `Name[...]`;
- `Exec`;
- `Icon`;
- `StartupWMClass`;
- `NoDisplay`;
- `Hidden`;
- Flatpak metadata keys when present.

Normalize `Exec=`:

- remove field codes such as `%U`, `%u`, `%F`, `%f`;
- identify wrapper commands;
- account for `env`;
- account for Flatpak launchers.

Cache index and watch for changes.

Do not rescan the entire desktop-file tree for every new audio stream.

---

# 15. Flatpak resolution

Flatpak apps are a first-class case.

Use available evidence such as:

- application ID;
- desktop file;
- process metadata;
- cgroup/process context if required;
- Flatpak-specific environment metadata.

Canonical identity should prefer:

```text
flatpak:<app-id>
```

Use host-accessible application icons from installed desktop entries where possible.

Do not require a privileged portal solely for identity resolution.

---

# 16. Snap resolution

Snap support is secondary but should not be architecturally excluded.

If Snap is installed, resolve:

- snap name;
- desktop file;
- command wrappers.

Failure to identify a Snap package must degrade to executable identity, not break mixing.

---

# 17. Steam / Proton / Wine resolver

This subsystem deserves dedicated tests.

## 17.1 Problem

A Windows game under Proton may expose a runtime chain closer to:

```text
steam
pressure-vessel
proton
wine64-preloader
Game.exe
```

The user wants:

```text
Game Name
```

## 17.2 Evidence sources

Inspect only when relevant:

- process executable;
- process command line;
- parent process tree;
- selected environment keys;
- `STEAM_COMPAT_DATA_PATH`;
- `STEAM_COMPAT_CLIENT_INSTALL_PATH`;
- Steam app manifest data;
- executable paths inside compatdata;
- Wine target `.exe`;
- game install directory.

## 17.3 Steam library discovery

Discover Steam libraries using Steam's own library metadata.

Build an index:

```text
SteamAppId
Name
InstallDir
LibraryPath
Icon candidates
```

Cache it.

Watch for changes at low frequency or on explicit refresh.

## 17.4 Proton identity

Preferred canonical identity:

```text
steam:<app-id>
```

even if the immediate process is Wine.

## 17.5 Non-Steam Wine

If no Steam identity exists:

```text
wine:<prefix-hash>:<target-exe>
```

Display target executable filename until user mapping improves it.

Allow:

```text
Rename
Choose icon
Remember this mapping
```

---

# 18. Browser behavior

Default:

```text
Firefox
Chromium
Chrome
Brave
```

as one row per browser application.

Do not create one row per tab.

Optional advanced expansion may expose individual sessions/media titles if metadata makes them available.

A future "media source" display might show:

```text
Firefox
  YouTube - video title
```

but physical bindings must remain bound to Firefox unless the user explicitly selects a session-level advanced binding.

---

# 19. Unknown applications

Unknown streams must never appear as meaningless raw IDs.

Fallback display strategy:

1. process executable basename;
2. sanitized application name;
3. media name;
4. `Unknown audio application`.

Include an action:

```text
Identify…
```

User can choose:

- existing known application;
- custom name;
- custom icon;
- "remember this executable as …".

---

# 20. Aggregation rules

Default grouping key:

```text
resolved canonical ApplicationId
```

An application row aggregates all playback sessions with that identity.

Effective display volume:

- if all sessions are effectively equal: display that value;
- if sessions differ: display a mixed-state indicator;
- changing the slider sets them all to the chosen value.

Mixed state example:

```text
Firefox     ~52%
```

or a subtle split marker.

Do not silently average different stream levels and pretend they are identical.

---

# 21. Audio activity and meters

Meters are visual indicators, not measurement instruments.

Requirements:

- low CPU;
- no unnecessary allocations;
- update UI around 30 Hz maximum;
- backend may sample faster if technically necessary;
- throttle/coalesce before UI;
- meter can be disabled globally.

Row meter should indicate:

```text
which app is producing sound?
```

not professional RMS/LUFS values.

No recording of audio.

No audio samples leave the PipeWire processing path.

---

# 22. Output devices

Main view shows currently selected default output.

Example:

```text
OUTPUT
Speakers - Focusrite USB
[========------] 72%
```

Actions:

- change output device;
- change output volume;
- mute output.

Per-application preferred output belongs in app settings, not the default row layout.

---

# 23. Per-application preferred output

Optional post-1.0 or late-1.0 feature.

Setting:

```text
Preferred output:
- Follow system default
- Headphones
- Speakers
- HDMI
```

When a new stream appears:

- if policy is Follow system, do nothing;
- if a fixed target exists and is available, move stream;
- if target unavailable, fall back safely and expose status.

Do not continuously move streams back if the user manually reroutes them unless "enforce preferred output" is explicitly enabled.

---

# 24. Physical controller model

## 24.1 Controller concepts

```text
ControllerDevice
ControllerControl
ControllerBinding
ControllerFeedback
```

A controller may expose:

- absolute fader;
- rotary encoder;
- button;
- toggle;
- touch sensor;
- LED;
- motorized fader;
- display in future.

## 24.2 Binding target

A control maps to a `MixerTarget`.

Examples:

```text
Fader 1 -> DynamicRole.CurrentGame
Fader 2 -> app:discord
Fader 3 -> group:music
Fader 4 -> master
Button 1 -> mute CurrentGame
```

## 24.3 Binding modes

Absolute controls:

- Jump;
- Pickup/Soft Takeover.

Relative controls:

- Relative signed;
- Relative binary offset;
- encoder-specific modes.

Default for ordinary MIDI faders:

```text
Pickup
```

because it avoids abrupt volume jumps when software and physical positions differ.

## 24.4 MIDI Learn

Required workflow:

1. user clicks `Learn`;
2. Fadrio listens for eligible controller events;
3. user moves/presses a control;
4. Fadrio identifies device/channel/control;
5. user selects target;
6. binding saved;
7. immediate test feedback.

Never force a normal user to type CC numbers manually.

Advanced editor may expose raw data.

---

# 25. Dynamic roles

Dynamic roles are a major differentiator.

Initial roles:

```text
CurrentGame
Communications
Music
Browser
SystemSounds
Master
```

## 25.1 CurrentGame

Resolution heuristics:

1. active Steam/Proton game producing audio;
2. application classified as game and recently foreground;
3. user override.

Do not automatically classify every Steam process as a game.

`steamwebhelper` must not become CurrentGame.

## 25.2 Communications

Candidates:

- Discord;
- Teams;
- Slack calls if detectable;
- user-defined applications.

Classification should be editable.

## 25.3 Music

Candidates:

- Spotify;
- music players;
- user-defined applications.

Do not infer based solely on an audio stream's waveform.

---

# 26. Groups

User-defined groups:

```text
Gaming
Voice
Music
Browser
Media
```

Each group has:

- ID;
- name;
- icon;
- member rules;
- explicit members;
- dynamic members if enabled;
- group volume behavior.

### Group volume rule

Default group slider should set member applications to the chosen absolute level only if that is what the UI communicates.

Alternative professional behavior is relative scaling, but it is less intuitive.

Recommended v1:

```text
Group slider controls a virtual group gain factor
```

This preserves relative member volumes.

Example:

```text
Discord 70%
Teams   50%

Voice group factor 50%

effective:
Discord 35%
Teams   25%
```

However this introduces a two-layer volume model.

Therefore **do not ship group gain until the UI clearly communicates it**.

For early versions, groups may be organizational/controller-target categories whose fader sets members to a common level.

Record the chosen behavior in an ADR before implementation.

---

# 27. Profiles

Profile fields:

```text
Profile
- Name
- Application volume policies
- Active groups
- Controller mapping set
- UI pin ordering
- Optional output-device policy
```

Initial profile workflow:

```text
Default
Gaming
Work
Streaming
```

Manual switching first.

Automatic activation later.

---

# 28. Automation

Do not introduce a generic rules engine in 1.0.

Later supported triggers can be narrow:

```text
When application starts -> activate profile
When application exits -> return to previous/default profile
```

Avoid:

```text
if device X + day Y + network Z + audio state ...
```

Fadrio should not become Home Assistant for audio.

---

# 29. CLI

CLI executable:

```text
fadrioctl
```

Candidate commands:

```bash
fadrioctl apps
fadrioctl devices
fadrioctl controllers
fadrioctl get app spotify
fadrioctl set app spotify 35
fadrioctl mute app discord
fadrioctl unmute app discord
fadrioctl profile gaming
fadrioctl status
```

The CLI should communicate with the running Fadrio instance over D-Bus/IPC rather than opening a second independent PipeWire manager whenever possible.

---

# 30. D-Bus IPC

Recommended bus name:

```text
dev.fglabs.Fadrio
```

Object path:

```text
/dev/fglabs/Fadrio
```

Provide a small stable API.

Do not expose internal database models directly.

Potential methods:

```text
ListApplications
SetApplicationVolume
SetApplicationMute
ActivateProfile
GetActiveProfile
ListDevices
```

Signals:

```text
ApplicationAdded
ApplicationRemoved
ApplicationChanged
ActiveProfileChanged
```

Security:

- user session bus;
- no system bus;
- no root service.

---

# 31. Single-instance behavior

Only one primary GUI/mixer service should own normal app state per user session.

Launching `fadrio` again should:

- activate existing window, or
- toggle the mixer popup depending on CLI flag.

No duplicate tray icons.

No competing volume restore engines.

---

# 32. Startup behavior

Options:

```text
Start Fadrio when I sign in
Start minimized
Show tray icon
Restore previous window position
```

Preferred autostart implementation:

- XDG autostart `.desktop` initially;
- optional systemd user service only if a background architecture genuinely needs it.

Do not install a system-level daemon.

---

# 33. Main UI information architecture

Top-level areas:

```text
Mixer
Applications
Controllers
Profiles
Settings
```

The app opens to Mixer.

Do not open to a dashboard.

---

# 34. Main Mixer UI

## 34.1 Window form

Default approximate size at 100% scale:

```text
width: 360 px
height: 680 px
minimum width: 320 px
```

Resizable vertically.

Allow broader width for users who prefer larger labels.

Remember size.

## 34.2 Layout

```text
┌─────────────────────────────────────┐
│ Fadrio                   profile ▾ │
│─────────────────────────────────────│
│ OUTPUT                              │
│ 🔊 Headphones                 72%   │
│ ━━━━━━━━━━━━━━━●━━━━━━━━━━          │
│─────────────────────────────────────│
│ APPLICATIONS                        │
│                                     │
│ 🏎 Le Mans Ultimate            86%  │
│   ━━━━━━━━━━━━━━━━━●━━━━           │
│   [mute]                        ⋯   │
│                                     │
│ 💬 Discord                     68%  │
│   ━━━━━━━━━━━━━●━━━━━━━━           │
│   [mute]                        ⋯   │
│                                     │
│ 🎵 Spotify                     31%  │
│   ━━━━━━●━━━━━━━━━━━━━━━━          │
│   [mute]                        ⋯   │
│                                     │
│ 🌐 Firefox                     52%  │
│   ━━━━━━━━━━●━━━━━━━━━━━━          │
│   [mute]                        ⋯   │
│                                     │
│ + Pin application                  │
└─────────────────────────────────────┘
```

## 34.3 Row contents

Required:

- icon;
- display name;
- volume;
- slider;
- mute action;
- context menu.

Optional:

- activity meter;
- pin indicator;
- hardware binding indicator;
- output override indicator.

Do not show PID, node ID, or executable path in the normal row.

---

# 35. Row interaction

Single click on row empty area:

- selects/highlights only if selection is needed.

Slider:

- immediate change;
- keyboard accessible.

Mute icon:

- toggles app mute.

Context menu:

```text
Mute
Pin / Unpin
Remember this volume
Output...
Controller binding...
Application settings...
Hide
```

Advanced stream detail:

```text
Show audio streams
```

should not be a primary control.

---

# 36. Sorting

Default:

1. pinned order;
2. currently audible apps;
3. active/running remembered apps;
4. optionally recent silent apps.

User options:

```text
Pinned first
Active first
Alphabetical
Manual
```

Prevent constant jitter.

If apps become active/inactive rapidly, do not reorder while the user is dragging a slider.

Use a short debounce or freeze ordering during interaction.

---

# 37. Disappearing streams

When an application's final stream disappears:

- if pinned: remain;
- if configured to show running apps and process remains: remain as inactive;
- otherwise enter a grace period;
- fade state;
- remove after grace period.

Default grace:

```text
30 seconds
```

This prevents UI flicker from apps that recreate streams.

---

# 38. Application row personalization

Per app:

- custom display name;
- custom icon;
- accent override;
- pinned;
- hidden;
- fixed/default/remembered volume policy;
- output policy;
- controller shortcut.

Customizations persist against canonical application ID.

---

# 39. Icons

## 39.1 Resolution order

1. user override;
2. desktop entry icon;
3. PipeWire application icon-name;
4. PipeWire embedded icon if valid;
5. Steam game icon/art asset;
6. executable-associated icon if extractable;
7. generic category icon;
8. generic application icon.

## 39.2 Icon caching

Cache resolved raster images in:

```text
$XDG_CACHE_HOME/fadrio/icons/
```

Key by source identity + modification signature.

Do not copy copyrighted game artwork into the repository.

Runtime use of locally installed icons/artwork should be treated separately from redistributable project assets.

## 39.3 Repository icon rules

All bundled icons must have:

- known license;
- source file;
- attribution when required;
- SVG master when possible.

Do not commit random downloaded icons.

---

# 40. Visual design system

## 40.1 Tone

The application should feel:

- calm;
- modern;
- compact;
- tactile;
- readable;
- slightly technical but not technical-looking.

Avoid:

- neon gamer aesthetic;
- faux mixing-console skeuomorphism;
- glass everywhere;
- oversized cards;
- enormous whitespace;
- GNOME Settings imitation;
- tiny legacy utility widgets.

## 40.2 Shape

Suggested corner radii:

```text
window/surfaces: 10-14
rows: 8-10
buttons: 6-8
```

Use a consistent radius scale.

## 40.3 Spacing scale

```text
4
8
12
16
24
32
```

Avoid arbitrary per-control spacing.

## 40.4 Typography

Use system sans-serif.

Do not bundle proprietary fonts.

Approximate sizes:

```text
App title        18-20
Section label    11-12 semibold
App name         14-15
Percentage       12-13 tabular numerals if possible
Supporting text  12
```

## 40.5 Theme

Required:

```text
System
Light
Dark
```

Optional:

```text
OLED
```

Custom accent may be offered later.

Do not ship ten themes at launch.

---

# 41. Per-application accent color

Default: off or subtle.

Optional auto-accent can derive a muted color from the application icon.

Rules:

- accent must never reduce text contrast;
- accent should affect small indicators/slider fill, not entire row backgrounds;
- user can disable auto accents globally;
- custom accent override available.

Do not create flashing/changing accent based on meter level.

---

# 42. Tray experience

Tray click behavior configurable:

```text
Left click -> compact mixer popup
Right click -> menu
```

Compact popup:

- output/master;
- pinned applications;
- currently active apps;
- no navigation sidebar;
- link/button to open full window.

Do not reproduce full Settings inside the popup.

Tray menu:

```text
Open Fadrio
Mute all (optional)
Profile >
Controllers status
Quit
```

---

# 43. Controller configuration UI

Main Controllers page:

```text
Controllers
────────────────────────────────

KORG nanoKONTROL2
Connected

Fader 1      Current Game
Fader 2      Discord
Fader 3      Music
Fader 4      Master

Button 1     Mute Current Game
Button 2     Mute Discord

[ Learn a control ]  [ Add binding ]
```

Device card statuses:

```text
Connected
Disconnected
Needs attention
```

Never show disconnected hardware as an error unless required for the active profile.

---

# 44. MIDI Learn UI

Modal/sheet:

```text
Learn control

Move a fader, turn a knob, or press a button
on the controller you want to assign.

Listening…

Detected:
KORG nanoKONTROL2
Channel 1
CC 2
Value 76

Target:
[ Current Game ▼ ]

Mode:
[ Pickup ▼ ]

[ Save binding ]
```

Timeout should be cancellable.

Ignore MIDI clock, active sensing, and noisy irrelevant messages during learn by default.

---

# 45. Soft takeover

Required for absolute faders.

State:

```text
software = 0.25
hardware = 0.82
```

Until hardware crosses software:

- no volume change;
- UI may show a small ghost marker for physical position.

Once crossed:

```text
binding becomes engaged
```

This should be visually understandable without clutter.

---

# 46. Feedback-capable controllers

Architecture should support:

```text
volume changed in software
→ controller backend
→ motor fader / LED / ring update
```

Feedback must include loop suppression.

Do not treat echoed controller MIDI as a new user command.

Maintain event origin/correlation where necessary.

---

# 47. Accessibility

Minimum requirements:

- complete keyboard navigation;
- visible focus;
- screen-reader labels for all icon-only buttons;
- sliders expose name, value, min/max;
- mute state announced;
- no color-only state;
- high-contrast compatibility;
- scalable UI;
- controller configuration operable without a mouse where practical.

Keyboard examples:

```text
Tab        move focus
Left/Right adjust slider
Space      mute/unmute focused app
Enter      open app actions
```

Do not globally capture media keys by default.

---

# 48. Localization

Build localization infrastructure before strings become widespread.

Use resource files.

Initial language can be English only during early development, but no UI string should be scattered as unstructured literals.

Avoid sentence fragments assembled from several translated strings.

---

# 49. Persistence schema

Suggested tables:

```text
schema_migrations
settings
applications
application_evidence
application_aliases
application_profiles
groups
group_members
profiles
profile_app_settings
controllers
controller_bindings
controller_templates
devices
ui_state
```

## 49.1 applications

```text
id TEXT PRIMARY KEY
display_name TEXT NOT NULL
desktop_file_id TEXT NULL
executable_path TEXT NULL
flatpak_id TEXT NULL
steam_app_id TEXT NULL
wine_executable TEXT NULL
custom_name TEXT NULL
custom_icon TEXT NULL
pinned INTEGER NOT NULL
hidden INTEGER NOT NULL
remember_volume INTEGER NOT NULL
last_volume REAL NULL
default_volume REAL NULL
created_utc TEXT NOT NULL
updated_utc TEXT NOT NULL
```

Store schema versions and write deterministic migrations.

Never silently delete user mappings because a resolver algorithm changed.

---

# 50. Threading model

This must be designed explicitly.

## 50.1 Native PipeWire thread

PipeWire callbacks run within the backend's loop/thread.

Never call Avalonia UI from native callbacks.

Normalize native events and put them onto a managed event queue.

## 50.2 Managed state coordinator

One serialized coordinator owns live mixer state.

Options:

- `Channel<T>` + single consumer;
- actor-like service.

Recommended:

```text
Channel<AudioBackendEvent>
```

All backend mutations feed the coordinator.

This reduces race conditions when streams rapidly appear/disappear/change.

## 50.3 UI updates

Coordinator publishes immutable snapshots/diffs.

UI dispatches only presentation changes through Avalonia dispatcher.

Throttle meter updates separately.

---

# 51. Native ABI ownership rules

All exported strings/buffers need explicit ownership.

Prefer:

```text
caller-provided callback receives immutable data valid for callback duration
```

or copy into bridge-defined structures.

Never require C# to free memory with an allocator different from the native library's allocator.

Define:

- versioned ABI;
- struct `size` fields;
- UTF-8;
- explicit booleans/integers;
- no C++ ABI;
- no exposed PipeWire pointers.

---

# 52. Backend event model

Possible events:

```text
BackendReady
BackendDisconnected
BackendReconnected
SessionAdded
SessionChanged
SessionRemoved
DeviceAdded
DeviceChanged
DeviceRemoved
DefaultOutputChanged
ControllerAdded
ControllerRemoved
ControllerEvent
```

Every runtime object gets a backend-generation token to prevent stale events from an old reconnect instance mutating new state.

---

# 53. PipeWire reconnect behavior

PipeWire may restart.

Fadrio must:

1. detect disconnection;
2. keep user configuration;
3. mark runtime state temporarily unavailable;
4. reconnect with backoff;
5. rebuild registry;
6. resolve applications again;
7. reapply only policies that are safe to restore.

UI:

```text
Audio service reconnecting…
```

not a crash.

---

# 54. Failure handling

Examples:

### PipeWire unavailable

Show:

```text
Fadrio could not connect to PipeWire.
```

Offer diagnostics.

Do not silently fall back to PulseAudio and change semantics unless a deliberate compatibility backend exists.

### Icon missing

Use fallback icon.

### `/proc` process gone

Continue with PipeWire evidence.

### Steam metadata unavailable

Use executable identity.

### Controller disconnects

Keep binding and mark device disconnected.

### SQLite migration fails

Do not start destructive reset.

Offer log/export path.

---

# 55. Privacy and security

Default:

- no account;
- no cloud;
- no telemetry;
- no audio recording;
- no microphone capture for mixer control;
- no root;
- no network listener;
- no automatic upload of logs.

If a local HTTP/WebSocket API is added later:

- disabled by default;
- bind to loopback only by default;
- explicit authentication/token if non-loopback is ever supported.

Process inspection:

- read only metadata required for application identity;
- avoid persisting command lines/environment variables;
- persist derived identity, not raw sensitive process context.

---

# 56. Diagnostics

Diagnostics page can show:

```text
PipeWire connection: Connected
PipeWire remote: pipewire-0
Audio sessions: 6
Resolved applications: 4
MIDI devices: 1
Database schema: 3
Native ABI: 1
```

Application diagnostics:

```text
Discord
Canonical ID: xdg:discord.desktop
Confidence: High
Executable: /usr/bin/discord
Desktop file: discord.desktop
PipeWire app id: ...
Sessions: 2
```

Provide copy/export button with redaction.

---

# 57. Testing strategy

## 57.1 Core unit tests

Test:

- identity canonicalization;
- resolver evidence scoring;
- grouping;
- volume policies;
- soft takeover;
- controller mapping;
- profile switching;
- sorting;
- grace-period removal.

## 57.2 Fixture-based Linux resolver tests

Do not require real `/proc` or Steam installations for every test.

Fixture structure:

```text
tests/fixtures/proc/...
tests/fixtures/steam/...
tests/fixtures/desktop-files/...
tests/fixtures/pipewire/...
```

Abstract filesystem/process metadata access.

## 57.3 Native tests

C tests for:

- lifecycle;
- object mapping;
- event translation;
- volume command serialization;
- reconnect;
- memory ownership.

Use sanitizers in CI where available.

## 57.4 Integration tests

Run against a real PipeWire test environment when practical.

Scenarios:

- start audio stream;
- detect application;
- set volume;
- mute;
- destroy stream;
- recreate;
- PipeWire restart;
- multiple streams same app.

## 57.5 Hardware tests

Create a virtual MIDI fixture/backend.

CI must not require physical MIDI hardware.

Manual qualification matrix should include at least:

- generic MIDI fader;
- rotary controller;
- one bidirectional device when supported.

---

# 58. Manual qualification matrix

For each release test:

Desktop/session:

- GNOME Wayland + XWayland;
- KDE Plasma Wayland;
- X11 session where available.

Distributions:

- Ubuntu/Zorin family;
- Fedora;
- Arch-based;
- Debian stable/current.

Packaging:

- tarball;
- Flatpak;
- .deb if offered;
- AppImage if offered.

Application cases:

- Firefox;
- Chromium;
- Spotify or equivalent;
- Discord;
- VLC;
- native Steam game;
- Proton game;
- non-Steam Wine app;
- Flatpak app.

---

# 59. UI testing

Use screenshot/golden testing cautiously.

Prefer view-model/component tests for behavior.

For visual regression:

- fixed theme;
- fixed scale;
- deterministic fake applications;
- deterministic icon fixtures.

Test:

- 100%;
- 125%;
- 150%;
- 200% scaling.

Pay special attention to the user's expected 4K/150% Linux environment.

---

# 60. Performance budgets

Targets, not guarantees:

Idle CPU:

```text
< 0.5% average on a modern desktop when no meters are active
```

Idle memory:

```text
keep practical for a resident desktop utility; target <150 MB private working set
```

Startup-to-functional mixer:

```text
aim < 1 second on normal SSD systems
```

UI meter refresh:

```text
30 Hz maximum
```

Do not poll PipeWire topology every second.

Use events.

---

# 61. Repository rules

## 61.1 Branches

```text
main
feature/<short-name>
fix/<short-name>
docs/<short-name>
release/<version>
```

`main` must remain buildable.

## 61.2 Commit style

Recommended conventional-ish style without enforcing excessive ceremony:

```text
feat: add application identity resolver
fix: preserve MIDI binding after reconnect
ui: add compact application row
native: handle PipeWire registry removal
docs: document Proton resolution
test: add multi-stream Firefox fixture
build: pin Avalonia dependencies
```

Keep commits coherent.

Do not mix broad formatting changes with feature changes.

## 61.3 Pull requests

Require:

- explanation;
- screenshots for visible UI changes;
- tests or justification;
- packaging impact if relevant;
- accessibility note for new UI controls.

---

# 62. Coding rules

## 62.1 C#

Enable:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AnalysisLevel>latest</AnalysisLevel>
```

Use file-scoped namespaces.

Prefer immutable records for snapshots/events.

Avoid static global mutable state.

Use dependency injection selectively, not as an abstraction contest.

No service locator.

## 62.2 Async

Do not use `.Result` or `.Wait()` in application/UI paths.

Pass `CancellationToken` for long-running operations.

Do not create uncontrolled `Task.Run` calls for every backend event.

## 62.3 Exceptions

Exceptions indicate exceptional failures, not normal stream disappearance.

Expected races such as `/proc/<pid>` disappearing should return a typed failure/result.

## 62.4 Native C

Compile with strong warnings.

Use:

```text
-Wall -Wextra -Wpedantic
```

and stricter flags where clean.

Use ASAN/UBSAN in test builds.

No unchecked ownership ambiguity.

---

# 63. Source formatting

Use `.editorconfig`.

C#:

- 4 spaces;
- braces consistent;
- max readability over artificial line limits.

C/CMake:

- 4 spaces;
- no tabs in source;
- C11 or newer.

Markdown:

- meaningful headings;
- fenced code;
- avoid generated TOCs committed unless tooling owns them.

---

# 64. Dependency policy

Central package version management:

```text
Directory.Packages.props
```

Rules:

- pin direct dependencies;
- review release notes before major upgrades;
- prefer mature libraries with active maintenance;
- avoid tiny abandoned packages for critical native integration;
- keep dependency count low.

Automated update PRs allowed, but never auto-merge UI/native/audio dependencies without CI + review.

---

# 65. Build system

Managed:

```bash
dotnet build
dotnet test
```

Native:

```bash
cmake -S src/native/fadrio-native -B build/native
cmake --build build/native
ctest --test-dir build/native
```

Top-level script:

```bash
./scripts/build.sh
```

should build native first, then managed projects with the resulting library available.

Do not require developers to memorize manual copy commands.

---

# 66. global.json

Pin expected .NET SDK feature band.

Permit a sane roll-forward policy.

The repository should fail with a clear message if an unsupported SDK is used.

---

# 67. CI

GitHub Actions stages:

## build.yml

- restore;
- build managed;
- build native;
- upload diagnostics on failure.

## test.yml

- managed unit tests;
- fixture tests;
- native unit tests;
- sanitizer job where available.

## package.yml

- build self-contained/release artifacts;
- test package contents;
- smoke launch where feasible.

## release.yml

Triggered by version tag.

Must:

- build from tag;
- generate checksums;
- produce SBOM if practical;
- attach artifacts;
- never reuse arbitrary local binaries.

---

# 68. Packaging strategy

Recommended priority:

1. tar.gz portable install;
2. Flatpak;
3. .deb;
4. AppImage;
5. RPM later if demand justifies.

## 68.1 Tarball

Useful for developers and direct downloads.

Include:

```text
fadrio
libfadrio_native.so
desktop file
icons
install.sh
uninstall.sh
licenses
```

User-level install preferred.

## 68.2 Flatpak

Important Linux distribution channel.

Permissions must be minimized.

PipeWire socket/audio access as required.

Controller access may require careful device permissions; qualify before advertising full hardware compatibility inside Flatpak.

If Flatpak sandbox limitations degrade MIDI/HID support, document them clearly.

## 68.3 .deb

Useful for Ubuntu/Zorin/Debian users.

Package native dependencies appropriately.

Avoid embedding distro-specific system libraries unnecessarily.

---

# 69. Release versioning

Use SemVer:

```text
0.1.0
0.2.0
...
1.0.0
```

Before 1.0, schema/API changes are allowed but migrations still must protect user state.

After 1.0, controller profile formats and D-Bus behavior need compatibility discipline.

---

# 70. Proposed roadmap

## 0.1 - Audio proof

- connect PipeWire;
- enumerate playback sessions;
- change volume;
- mute;
- fake basic vertical UI;
- no persistence;
- no fancy identity.

Success criterion:

```text
real apps can be controlled reliably without shelling out to pactl
```

## 0.2 - Application identity

- `/proc`;
- XDG desktop index;
- icon resolver;
- application grouping;
- stable canonical IDs.

Success criterion:

```text
Firefox appears once with correct icon
```

## 0.3 - Persistence

- SQLite;
- pin/hide;
- remember volume;
- stable ordering;
- grace removal.

## 0.4 - Steam/Proton

- Steam library index;
- Proton process resolution;
- game name/icon;
- CurrentGame role prototype.

## 0.5 - UI alpha

- polished main mixer;
- tray popup;
- settings;
- themes;
- accessibility baseline.

## 0.6 - MIDI

- enumerate device;
- MIDI Learn;
- absolute faders;
- buttons;
- pickup mode;
- save bindings.

## 0.7 - Profiles/groups

- groups;
- manual profiles;
- controller mapping sets.

## 0.8 - Reliability

- PipeWire restart;
- controller reconnect;
- migration tests;
- packaging matrix;
- diagnostics.

## 0.9 - Public beta

- docs;
- onboarding;
- translation infrastructure;
- hardware qualification;
- performance pass.

## 1.0

Ship only when:

- application identity is reliable;
- Proton games resolve acceptably;
- mixer survives PipeWire restarts;
- MIDI bindings survive reconnects;
- no destructive database migration path;
- UI works at fractional scaling;
- installation/removal are clean;
- docs accurately describe limitations.

---

# 71. v1 onboarding

First launch should be very short.

Screen 1:

```text
Fadrio
Control the volume of applications, not audio streams.
```

Show detected apps.

Screen 2, optional:

```text
Have a physical MIDI controller?
[ Set it up ] [ Later ]
```

Done.

Do not force account creation.

Do not ask users to configure PipeWire.

---

# 72. Settings structure

```text
General
Appearance
Audio
Applications
Controllers
Profiles
Advanced
About
```

General:

- launch at login;
- start minimized;
- tray behavior;
- inactive app grace period.

Appearance:

- theme;
- density;
- percentages;
- meters;
- auto accent.

Audio:

- amplification >100%;
- default output;
- volume restore behavior.

Advanced:

- backend diagnostics;
- native Wayland experimental toggle when applicable;
- logging level;
- export diagnostics;
- reset application resolver cache.

---

# 73. Reset behavior

Never provide one dangerous generic `Reset everything`.

Separate:

```text
Reset UI layout
Reset remembered volumes
Reset application names/icons
Reset controller mappings
Reset all Fadrio data
```

The destructive full reset must show exactly what is deleted.

---

# 74. Repository images and assets policy

## 74.1 Folders

```text
assets/branding/source/
assets/branding/exports/
assets/screenshots/
assets/mockups/
assets/controller-diagrams/
assets/store/
```

## 74.2 Source formats

Logo/icon master:

- SVG preferred;
- vector source tracked;
- PNG exports generated.

Screenshots:

- PNG;
- no JPEG unless photographic content requires it.

Store art:

- generated/exported from source;
- do not hand-edit final PNG only.

## 74.3 Naming

```text
fadrio-icon-master.svg
fadrio-icon-512.png
mixer-dark-150pct.png
controller-midi-learn.png
```

No:

```text
final2-new-real.png
```

## 74.4 Screenshots

For official screenshots:

- use synthetic/non-private app labels where licensing/privacy is uncertain;
- do not show personal notifications;
- use consistent scaling;
- capture both light/dark only when useful;
- keep raw screenshot and edited export separate if edits are needed.

## 74.5 Third-party logos

Do not bundle third-party application logos in the repository simply to make screenshots attractive.

Runtime-resolved installed app icons are different from redistributed assets.

For promotional mockups, follow the relevant trademark/logo rules.

---

# 75. Product icon brief

The icon should communicate:

```text
volume + vertical faders
```

without looking like a DAW.

Possible visual grammar:

- 3 vertical slider stems;
- simple rounded endpoints;
- subtle sound-wave or speaker implication;
- readable at 16 px;
- no text.

Avoid:

- microphone as dominant symbol;
- DJ turntables;
- equalizer with 20 bands;
- waveform logo that implies editing;
- copied Windows mixer icon.

Create:

```text
16
24
32
48
64
128
256
512
1024
```

as raster exports where packaging requires them.

---

# 76. Documentation set

README should answer:

1. what it is;
2. screenshot;
3. why it exists;
4. supported stack;
5. install;
6. controller support;
7. current limitations.

Architecture docs should explain mechanisms.

User docs should not expose internal architecture unless useful.

Do not make README a 5,000-line technical specification; link `TECH_SPEC.md`.

---

# 77. Issue templates

Bug issue fields:

- Fadrio version;
- distribution;
- desktop environment;
- Wayland/X11;
- PipeWire version;
- package type;
- application affected;
- expected/actual;
- diagnostics file optional.

Controller issue fields:

- manufacturer;
- model;
- USB ID if known;
- MIDI input/output ports;
- controls;
- feedback capability;
- package type;
- logs.

Do not ask users to paste their entire environment.

---

# 78. Security policy

`SECURITY.md` should provide private disclosure route.

Relevant security areas:

- D-Bus API abuse;
- native memory safety;
- malicious `.desktop` parsing;
- untrusted icon/image parsing;
- controller input floods;
- local API if added later;
- path traversal in imported profiles;
- SQLite corruption handling.

---

# 79. Imported/exported configuration format

If profile export exists:

```json
{
  "format": "fadrio-profile",
  "version": 1,
  "profile": { }
}
```

Validate strictly.

Never allow imported profile files to write arbitrary paths outside Fadrio data directories.

Icons embedded in exports should have size/type limits.

---

# 80. Controller template format

Future community template:

```json
{
  "format": "fadrio-controller-template",
  "version": 1,
  "device": {
    "manufacturer": "Example",
    "name": "Eight Faders"
  },
  "match": {
    "midiPortRegex": "..."
  },
  "controls": []
}
```

Templates should describe physical controls, not force user application names.

Recommended mapping can target semantic roles:

```text
CurrentGame
Communications
Music
Browser
SystemSounds
Master
```

This makes templates reusable.

---

# 81. Controller safety

Hardware faders can produce sudden volume changes.

Default protections:

- pickup mode;
- optional max volume;
- ignore malformed events;
- rate-limit feedback;
- no amplification >100% unless enabled.

If a device reconnects at a different physical position, do not immediately jump software volume.

---

# 82. Volume persistence policy

Recommended settings per app:

```text
Follow current PipeWire value
Remember last volume
Always start at fixed volume
```

Default:

```text
Remember last volume
```

but only apply when a new session is confidently resolved to the app.

If identity confidence is Low/Unknown, avoid automatically applying stored settings for another potentially unrelated executable.

---

# 83. Resolver conflict handling

If evidence conflicts:

Example:

```text
PipeWire says: Discord
Executable says: electron
Desktop match says: Slack
```

Do not guess with false confidence.

Use confidence scoring and diagnostic evidence.

If ambiguous, create a safe runtime identity and ask the user only when a persistent mapping is actually useful.

---

# 84. Application identity user override

UI:

```text
This audio is currently identified as:
wine64-preloader

Treat it as:
[ Le Mans Ultimate ▼ ]

[ Remember ]
```

Store override against stable evidence pattern, not transient PID.

Provide a way to remove override.

---

# 85. System sounds

Aggregate known desktop sound services into:

```text
System Sounds
```

only when grouping evidence is reliable.

Never hide arbitrary unknown applications under System Sounds merely to make the UI clean.

---

# 86. App classification

Optional categories:

```text
Game
Communications
Music
Browser
Media
System
Other
```

Classification can be:

- known built-in metadata;
- user override;
- Steam identity;
- desktop category hints.

Never use AI/cloud classification.

Local deterministic rules are sufficient.

---

# 87. "Current Game" detection

Candidate algorithm:

1. list active resolved applications classified as Game;
2. prefer audible applications;
3. prefer app with recent foreground evidence if foreground detection backend exists;
4. prefer Steam game over Steam client;
5. retain previous current game during short stream recreation gaps;
6. allow manual lock.

Do not make foreground-window detection a hard dependency for v1.

---

# 88. Window/foreground detection

Optional Linux service.

Because Wayland restricts arbitrary global window inspection, do not architect core identity around being able to read all windows.

Use it only as optional evidence where available.

No X11-only assumption in core behavior.

---

# 89. WirePlumber relationship

Fadrio is a PipeWire client, not a replacement session manager.

Use WirePlumber-managed graph behavior rather than competing with it.

Do not ship system WirePlumber rules unless a narrowly justified optional feature requires them.

---

# 90. Why not PulseAudio as the main abstraction

PipeWire's PulseAudio compatibility is valuable for compatibility, but Fadrio wants:

- modern graph awareness;
- native object events;
- long-term Linux desktop direction;
- direct PipeWire metadata.

Therefore:

```text
PipeWire native = production
Pulse/pactl = prototype/diagnostic compatibility only
```

---

# 91. Why not shell commands in production

Do not implement production volume control as:

```text
Process.Start("wpctl ...")
```

Problems:

- parsing;
- localization/output changes;
- process spawning overhead;
- races;
- weak event model;
- difficult reconnect handling;
- poor controller latency.

Shell tooling remains invaluable for diagnostics.

---

# 92. Why not make Rust the entire app

Rust would be technically strong for PipeWire/native Linux work.

It is not chosen as the entire stack because:

- product velocity matters;
- UI development would require a different stack;
- the user already has mature Avalonia/.NET experience;
- a small native C boundary captures most low-level benefit without moving the entire product.

Revisit only if:

- C# native interop becomes a dominant maintenance burden;
- Avalonia becomes a Linux UX blocker;
- low-level controller support grows far beyond expectations.

---

# 93. Why C rather than C++ for native bridge

Use C for the exported bridge because:

- PipeWire is C;
- stable ABI;
- simple P/Invoke;
- no C++ standard-library ABI concerns;
- easier ownership rules.

Internal C++ is not necessary.

---

# 94. Why not a separate daemon at first

A daemon + UI split adds:

- lifecycle;
- IPC;
- package complexity;
- crash coordination;
- state synchronization.

Start as one user application process with internal services.

Use D-Bus so CLI/integrations can communicate with the running app.

Split a background service later only if there is a concrete requirement such as persistent hardware control with no GUI process.

---

# 95. Data migration rules

Every schema change:

- has a numbered migration;
- is tested from previous release schema;
- is transactional where possible;
- never silently drops user bindings/mappings.

Before 1.0, still maintain migrations for real beta users.

---

# 96. Crash behavior

If UI crashes but native backend lives in-process, process ends.

On restart:

- recover database;
- reconnect;
- rebuild runtime state.

Do not attempt exotic crash-resident helpers before 1.0.

Persist user choices promptly enough that a crash does not lose recent controller mappings.

---

# 97. Telemetry policy

Recommendation:

```text
No telemetry in v1.
```

If analytics are ever proposed:

- explicit opt-in;
- document fields;
- no application names by default because app lists reveal user behavior;
- no audio metadata;
- no command lines;
- no controller serial numbers.

---

# 98. Update mechanism

For early releases:

- GitHub/website update notification;
- package-manager update for Flatpak/.deb.

Do not build an auto-updater before packaging strategy stabilizes.

For tarball builds, an in-app notification may link to release page.

Never overwrite system packages installed by apt/Flatpak.

---

# 99. License strategy

Recommended:

```text
GPL-3.0-or-later
```

if the goal is a strongly open-source Linux utility and contributions should remain open.

Alternative:

```text
MPL-2.0
```

if file-level copyleft is preferred.

Alternative:

```text
MIT
```

if maximal reuse is strategically important.

Choose before accepting external contributions.

Include third-party notices for:

- Avalonia;
- native dependencies;
- icon libraries;
- any bundled controller definitions.

---

# 100. Definition of done for a feature

A feature is done when:

- functionality works;
- failure path works;
- relevant unit/integration tests exist;
- keyboard interaction works for UI;
- screen-reader label exists;
- logs are useful but not noisy;
- docs updated if behavior/user workflow changed;
- screenshots updated if public UI materially changed;
- packaging implications considered;
- no raw PipeWire/process terminology leaks into normal UI unless intentionally advanced.

---

# 101. Initial implementation order

The recommended coding order is deliberately not "make the pretty UI first."

### Stage A - Native proof

Create `libfadrio_native.so`.

Prove:

```text
connect
enumerate
listen
set volume
mute
reconnect
```

### Stage B - Managed backend model

Translate native events into:

```text
AudioSession
AudioDevice
```

Use fake backend tests.

### Stage C - Identity index

Implement:

```text
/proc
desktop files
icons
canonical IDs
```

### Stage D - Aggregation

Implement:

```text
many sessions -> one app
```

### Stage E - Minimal UI

Now create real app rows.

### Stage F - persistence

Pin, hide, remember.

### Stage G - Steam/Proton

Before public gaming claims.

### Stage H - controller abstraction + fake controller

Build mappings before physical MIDI.

### Stage I - ALSA MIDI backend

Then MIDI Learn.

### Stage J - polish/reliability/package.

---

# 102. First technical spike checklist

Create a console prototype that prints:

```text
Application: Firefox
Canonical: xdg:firefox.desktop
Sessions: 2
PIDs: 1234, 1260
Volume: 0.52
Muted: false
Icon: firefox
```

Then run:

```text
set Firefox 0.30
```

and verify all owned sessions change.

Repeat with:

- native app;
- Firefox;
- Discord;
- Proton game.

If application grouping cannot be made robust, stop and fix it before investing in visual polish.

---

# 103. First controller spike checklist

Fake target:

```text
CurrentGame
```

Connect MIDI device.

Implement:

```text
CC -> normalized 0..1
pickup
set target
```

Test disconnect/reconnect.

Then add MIDI Learn.

Do not begin with motorized faders.

---

# 104. Suggested ADRs

Create architecture decision records for contentious choices.

Start with:

```text
ADR-0001 Use .NET + Avalonia
ADR-0002 Native C bridge for PipeWire
ADR-0003 Application identity is primary UI abstraction
ADR-0004 SQLite persistence
ADR-0005 MIDI through Linux-native backend
ADR-0006 No audio DSP/routing scope
ADR-0007 Single-process architecture for v1
ADR-0008 XWayland production baseline until native Wayland qualifies
```

---

# 105. CI quality gates

PR cannot merge if:

- build fails;
- unit tests fail;
- native tests fail;
- formatting/analyzers fail;
- generated package smoke test fails on release branch.

Do not require 100% code coverage.

Track meaningful coverage on:

- resolver;
- persistence migrations;
- controller mapping;
- backend state transitions.

---

# 106. Release qualification scenarios

Before 1.0 execute:

## Scenario A - browser

1. start Firefox;
2. play two media sources;
3. confirm one Firefox row;
4. change volume;
5. stop one;
6. row remains stable;
7. restart Firefox;
8. remembered volume restored.

## Scenario B - Proton game

1. start Steam;
2. start game;
3. confirm no `wine64-preloader` row;
4. correct game name/icon;
5. CurrentGame mapping follows game;
6. exit;
7. start different game;
8. same physical CurrentGame fader now controls new game.

## Scenario C - controller

1. start at software 25%;
2. reconnect fader physically at 80%;
3. volume does not jump;
4. move toward 25%;
5. pickup engages;
6. volume follows.

## Scenario D - PipeWire restart

1. apps playing;
2. restart PipeWire/user service;
3. Fadrio shows temporary reconnect state;
4. reconnects;
5. applications reappear;
6. no duplicate rows;
7. saved mappings preserved.

---

# 107. Public positioning

Recommended wording:

> **Fadrio is an application volume mixer for Linux.**
>
> It turns PipeWire streams into the applications you actually recognize, gives each one a clean volume control, and lets you map apps or roles such as Game, Chat, Music, and Master to physical MIDI faders.

Avoid:

```text
Professional Linux audio workstation
Ultimate PipeWire manager
Voicemeeter alternative
```

Those attract the wrong expectations.

---

# 108. Screenshot concept

Primary product screenshot should show:

```text
Headphones 72%

Le Mans Ultimate 86%
Discord 68%
Spotify 31%
Firefox 52%
```

with recognizable but legally appropriate runtime/sample icons.

Second screenshot:

```text
Controller mapping
Fader 1 -> Current Game
Fader 2 -> Discord
Fader 3 -> Music
Fader 4 -> Master
```

Third screenshot:

```text
Application identity/personalization
```

Do not lead with Settings.

---

# 109. README first paragraph

Suggested:

> Fadrio is a simple application volume mixer for Linux. Instead of exposing raw audio streams and process names, it groups PipeWire audio into the applications you recognize and gives each one a clean, persistent volume control. Applications and semantic roles such as Current Game, Communications, Music, and Master can also be mapped to MIDI faders, knobs, and buttons.

---

# 110. Product boundaries checklist for future requests

Before accepting a new feature ask:

1. Does it help identify an application?
2. Does it help control application/device volume?
3. Does it help persist or organize those controls?
4. Does it help a physical/software controller operate those controls?
5. Does it improve reliability/accessibility/presentation of the above?

If all are **no**, it probably belongs elsewhere.

---

# 111. External API evolution

D-Bus and exported profile formats should have explicit versions.

Native ABI:

```text
VM_NATIVE_ABI_VERSION
```

Managed interop must fail clearly if incompatible native library is found.

Do not rely on "same package means same version" as the only protection.

---

# 112. App shutdown

Order:

1. stop accepting UI/controller commands;
2. unsubscribe/stop controller backends;
3. flush persistence;
4. disconnect managed backend;
5. stop PipeWire loop;
6. dispose native context;
7. exit.

Do not hang indefinitely if PipeWire is already gone.

---

# 113. Importantly: what not to overengineer in 0.x

Do not build before demand:

- plugin marketplace;
- scripting language;
- cloud sync;
- account system;
- remote phone app;
- WebSocket API;
- controller community store;
- motorized fader protocol matrix;
- dozens of profiles;
- custom CSS editor;
- AI app classification.

Keep interfaces extensible but implementations narrow.

---

# 114. Recommended initial backlog

### Epic: Native audio

- PipeWire connect
- registry
- playback stream model
- device model
- volume/mute
- reconnect
- meter path

### Epic: Identity

- `/proc` provider
- desktop file index
- icon resolver
- canonical ID
- aggregation
- user override
- Flatpak
- Steam/Proton

### Epic: Mixer UI

- shell
- output row
- app row
- row states
- sorting
- pin/hide
- tray popup
- app settings

### Epic: Persistence

- database
- migrations
- volume policy
- UI state
- identity evidence

### Epic: Controllers

- abstraction
- fake backend
- ALSA MIDI
- MIDI Learn
- binding editor
- pickup
- reconnect
- feedback architecture

### Epic: Delivery

- build scripts
- CI
- tarball
- Flatpak
- .deb
- diagnostics
- docs
- accessibility
- localization

---

# 115. Suggested repository bootstrap commands

```bash
mkdir Fadrio
cd Fadrio

dotnet new sln -n Fadrio

dotnet new classlib -n Fadrio.Core -o src/Fadrio.Core
dotnet new classlib -n Fadrio.Application -o src/Fadrio.Application
dotnet new classlib -n Fadrio.Infrastructure -o src/Fadrio.Infrastructure
dotnet new classlib -n Fadrio.Platform.Linux -o src/Fadrio.Platform.Linux
dotnet new classlib -n Fadrio.NativeInterop -o src/Fadrio.NativeInterop
dotnet new console -n Fadrio.Cli -o src/Fadrio.Cli
```

Create the Avalonia app using the currently supported Avalonia templates/version selected for the repository.

Native:

```bash
mkdir -p src/native/fadrio-native/{include,src,tests}
```

Then wire dependencies intentionally rather than `dotnet add reference` everywhere.

---

# 116. Package references policy

Do not put application dependencies directly into every project.

Examples:

`Core`:

```text
ideally no external runtime dependencies
```

`Application`:

```text
Microsoft.Extensions.*
```

`Infrastructure`:

```text
Microsoft.Data.Sqlite
```

`UI`:

```text
Avalonia
Avalonia.Desktop
Avalonia.Themes.*
```

Native interop:

```text
no third-party managed PipeWire wrapper required
```

---

# 117. Development dependencies on Debian/Ubuntu family

Indicative packages:

```bash
sudo apt install \
  build-essential \
  cmake \
  ninja-build \
  pkg-config \
  libpipewire-0.3-dev \
  libspa-0.2-dev \
  libasound2-dev
```

Exact package names must be maintained in `docs/development/setup-linux.md` and CI images.

Do not embed this list in five different docs.

---

# 118. Debug commands

Useful developer diagnostics:

```bash
wpctl status
pw-cli ls Node
pw-dump
```

Use them for comparison/debugging, not application behavior.

For process identity:

```bash
readlink /proc/<pid>/exe
tr '\0' ' ' < /proc/<pid>/cmdline
```

Never instruct users to run giant environment dumps unless necessary.

---

# 119. Source generators/code generation

Avoid unnecessary code generation.

Potential valid generation:

- native P/Invoke signatures from a small owned header;
- strongly typed localization resources.

Do not generate domain models from SQLite schema.

---

# 120. Database backups

This is not backup software, but user configuration is valuable.

Before a risky migration:

```text
copy current DB to a bounded migration backup
```

Keep one or two recent migration backups.

Do not accumulate unlimited copies.

---

# 121. App-data uninstall policy

Uninstall script/package removal should remove binaries/integration.

User configuration should remain unless user explicitly requests purge.

Document:

```text
Uninstall application
Remove user data
```

as separate actions.

---

# 122. Accessibility + hardware synergy

Physical controller support is also an accessibility feature.

Future UI can support:

- larger row mode;
- high contrast;
- permanent pinned controls;
- keyboard-only operation.

Do not market accessibility claims until tested, but architect accordingly.

---

# 123. Contribution policy

Contributions welcome in areas:

- application resolvers;
- distro packaging;
- controller templates;
- accessibility;
- translations;
- tests.

Require controller templates to include device identification and evidence of testing.

Do not merge unknown binary firmware/blobs into repository.

---

# 124. Binary asset policy

Repository should not store:

- firmware;
- proprietary controller software;
- proprietary app icons;
- arbitrary executable test samples.

For tests, use synthetic fixtures.

Use Git LFS only if genuinely required for large visual assets; avoid it for ordinary screenshots.

---

# 125. Localization keys

Use semantic keys:

```text
Mixer_Section_Applications
Mixer_App_Mute
Controller_Learn_Title
```

Avoid:

```text
TextBlock17
Label2
```

Keep translator comments where context matters.

---

# 126. UI animation rules

Animations:

- short;
- functional;
- cancellable;
- no bouncing.

Use for:

- app row appearing/disappearing;
- mute state;
- controller binding detected;
- profile transition.

Respect reduced-motion preference where available/configurable.

---

# 127. Notifications

Use notifications sparingly.

Good:

```text
MIDI controller disconnected
```

only if the active profile depends on it and user wants notifications.

Bad:

```text
Spotify volume changed to 42%
```

No notification spam.

---

# 128. Audio session history

Do not build a permanent "what you listened to" history.

That creates privacy concerns and is unnecessary.

Persist only what is needed for application identity/configuration.

---

# 129. State model recommendation

Use immutable public snapshots:

```csharp
MixerSnapshot
{
    Output
    Applications
    Controllers
    ActiveProfile
    BackendHealth
}
```

Internal services may be mutable.

Presentation receives snapshots/diffs.

This makes race-heavy backend behavior easier to reason about.

---

# 130. Command model

All control operations become commands:

```text
SetApplicationVolume
SetApplicationMute
SetDeviceVolume
ActivateProfile
ApplyControllerInput
```

Commands include origin:

```text
UI
MIDI
CLI
DBus
Profile
RestorePolicy
```

This supports diagnostics and feedback-loop suppression.

---

# 131. Event origin

For controller feedback:

```text
MIDI input
-> command origin MIDI(device/control)
-> mixer state change
-> feedback layer
-> suppress sending equivalent value back to same origin where needed
```

Do not use fragile global "ignore next event" booleans.

---

# 132. Testable clock

Use an injectable clock for:

- grace periods;
- reconnect backoff;
- activity decay;
- profile timing;
- controller learn timeout.

Do not sprinkle `DateTime.UtcNow` through domain logic.

---

# 133. Resolver cache invalidation

Invalidate when:

- desktop files change;
- Steam library metadata changes;
- user mapping changes;
- application executable path changes;
- relevant icon changes.

Do not recompute every identity on every meter event.

---

# 134. Icon loading security

Raster/vector parsing can be attack surface.

- restrict supported formats;
- cap dimensions/file sizes;
- use framework-safe decoders;
- do not render arbitrary remote URLs;
- no network icon downloading in v1.

Steam/local application art only from trusted local installation paths discovered by resolver.

---

# 135. Profile conflict resolution

If a profile sets 30% but app's remembered last volume is 55%:

Activation precedence:

```text
explicit active profile setting
> fixed per-app startup policy
> remembered last volume
> existing runtime volume
```

Document this.

On profile deactivation, initial behavior should not attempt complicated restoration stacks until deliberately implemented.

---

# 136. Multiple outputs / device changes

If default output changes:

- update output header;
- follow system for apps with no override;
- do not lose app settings.

If an output disappears:

- mark overrides unavailable;
- let WirePlumber/system policy route;
- do not repeatedly issue failing move commands.

---

# 137. Microphone support

Recommended after playback 1.0 unless demand is strong.

When implemented, place in a distinct `Input` section/page.

Do not mix capture app rows into playback list.

Do not process mic audio.

Control only:

- device level;
- mute;
- perhaps app capture stream levels if backend supports user-meaningful behavior.

---

# 138. Advanced stream inspector

A diagnostic/advanced sheet:

```text
Firefox

Sessions
- node 81 | YouTube | 52%
- node 94 | WebRTC | 52%

Identity
- xdg:org.mozilla.firefox
- confidence: high
```

This is where raw audio details belong.

---

# 139. Search

Application manager should support search.

Mixer itself generally does not need a persistent search box unless users have many pinned apps.

Optional keyboard shortcut:

```text
Ctrl+K
```

could open quick app selector later.

---

# 140. Keyboard shortcuts

Safe defaults:

```text
Ctrl+,  Settings
Ctrl+K  Find application
Ctrl+M  Mute selected application (optional)
```

Global shortcuts are off by default.

Wayland global hotkeys require platform-specific mechanisms/portals and must not be assumed universally available.

---

# 141. Controller quick-bind from app row

App context menu:

```text
Bind physical control…
```

Then Fadrio enters MIDI Learn and automatically chooses this app as target.

This should be one of the fastest workflows in the product.

---

# 142. First-run hardware discovery

If a MIDI device is detected on first launch:

show a subtle non-blocking suggestion:

```text
MIDI controller detected
Set up physical controls
```

Do not interrupt every launch.

---

# 143. Device matching

Persist controller identity using a combination of:

- backend;
- port/device name;
- manufacturer/product metadata when available;
- USB identity when reliably correlated;
- optional user-defined device alias.

Do not rely only on ALSA client number because it changes.

---

# 144. Controller reconnect

When device returns:

- re-enumerate;
- match saved identity;
- restore bindings;
- enter safe pickup/disengaged state for absolute controls;
- update feedback only after connection is stable.

---

# 145. Controller templates in 1.x

A template may define:

```text
Fader 1
Fader 2
Knob 1
Button 1
LED 1
```

and raw MIDI mappings.

User profile then maps semantic controls to Fadrio targets.

Separate:

```text
hardware layout
from
user mixer assignment
```

This is critical.

---

# 146. Error messages

Use user concepts.

Bad:

```text
SPA_PARAM_Props enumeration failed errno -32
```

Good:

```text
Fadrio lost its connection to the audio service and is reconnecting.
```

Detailed error available in diagnostics.

---

# 147. Empty states

No active audio:

```text
No applications are playing audio.

Pinned applications will stay here.
```

No MIDI:

```text
No MIDI controllers detected.

Connect a controller, then choose Refresh.
```

Never make an empty state look like an error.

---

# 148. About page

Show:

- version;
- build commit;
- .NET runtime;
- native ABI;
- PipeWire connection/version if available;
- license;
- links;
- diagnostics.

This helps support.

---

# 149. Build reproducibility

Tag builds should record:

```text
Version
Commit SHA
Build date
Native ABI
```

Avoid embedding absolute developer paths.

Generate SHA256 checksums for release assets.

---

# 150. Final architectural contract

If future implementation choices conflict with this document, preserve these contracts unless an ADR explicitly replaces them:

1. **The user controls applications, not streams.**
2. **Application identity is stable and evidence-based.**
3. **A single application may own multiple transient audio sessions.**
4. **Raw PipeWire topology stays below the normal UI.**
5. **PipeWire native API is the production audio backend.**
6. **Physical controllers use the same mixer command/state model as the GUI.**
7. **MIDI is the first external-controller protocol.**
8. **No root/system daemon is required.**
9. **No audio recording or DSP is performed.**
10. **Fadrio does not become a routing workstation.**
11. **User configuration survives process/audio/controller restarts.**
12. **Steam/Proton/Wine resolution is a product feature, not an edge-case hack.**
13. **The app must be pleasant at fractional scaling and modern Wayland desktops even if the production renderer currently operates through XWayland.**
14. **The repository must keep native/audio logic isolated from UI logic.**
15. **Every persistent format and native/IPC boundary is versioned.**

---

# 151. Reference notes used for the initial technical choices

At the time this specification was drafted:

- PipeWire documentation exposes logical application metadata including `application.name`, `application.id`, `application.icon-name`, `application.process.id`, and `application.process.binary`, which directly supports the resolver strategy.
- PipeWire 1.4 documentation describes the native client/library architecture used by the recommended backend.
- Avalonia's current Linux documentation identifies X11 as the primary production Linux backend and a newer native Wayland backend as opt-in/experimental; therefore Fadrio should qualify Linux/Wayland behavior rather than basing v1 on an experimental rendering path.
- ALSA MIDI remains a normal Linux MIDI backend, while cross-platform libraries such as RtMidi demonstrate the expected abstraction around input/output ports. Fadrio's Linux-first implementation can use ALSA directly while retaining an interface suitable for later backends.

These external details should be revalidated when implementation starts and dependency versions are pinned.

---

# 152. Immediate next action

The first repository milestone should be:

```text
M0 - PipeWire Application Identity Spike
```

Deliverables:

- solution/repo scaffold;
- native C library;
- managed event bridge;
- console app;
- desktop-file index;
- `/proc` resolver;
- one native app test;
- one browser test;
- one Proton game test;
- no polished UI yet.

Definition of success:

> Starting three real applications produces three stable logical application identities, and setting one logical application's volume changes every one of its owned PipeWire playback sessions without affecting the others.

Only after that milestone is reliable should the full visual mixer be built.

---

# Appendix A - Recommended repository file responsibilities

## `AGENTS.md`

Rules for AI-assisted/code-assistant changes:

- preserve architecture boundaries;
- never replace native PipeWire code with shell parsing without explicit request;
- never expose raw process IDs in normal UI;
- add tests for resolver changes;
- do not silently add network/telemetry behavior;
- keep patches scoped;
- update docs/ADR for architecture changes.

## `Directory.Build.props`

Central compiler/analyzer defaults.

## `Directory.Packages.props`

Central NuGet versions.

## `global.json`

Pinned .NET SDK.

## `docs/decisions/`

Every major reversal gets an ADR.

---

# Appendix B - Suggested project references

```text
Fadrio.Core
  -> none

Fadrio.Application
  -> Fadrio.Core

Fadrio.Infrastructure
  -> Fadrio.Core
  -> Fadrio.Application

Fadrio.Platform.Linux
  -> Fadrio.Core
  -> Fadrio.Application

Fadrio.NativeInterop
  -> Fadrio.Core

Fadrio.UI
  -> Fadrio.Core
  -> Fadrio.Application
  -> Fadrio.Infrastructure
  -> Fadrio.Platform.Linux
  -> Fadrio.NativeInterop

Fadrio.Cli
  -> preferably IPC contract only
```

Refine composition-root dependencies if DI setup moves to a dedicated executable project.

---

# Appendix C - UI state examples

## Normal

```text
Le Mans Ultimate   86%  playing
Discord            68%  playing
Spotify            31%  playing
Firefox            52%  silent
```

## Mixed stream levels

```text
Firefox            ~52%
```

## Resolver uncertain

Normal row uses best safe display.

Context/diagnostic menu can show:

```text
Identity uncertain
Review mapping
```

## PipeWire reconnect

Rows remain visually present but disabled/faded:

```text
Reconnecting to audio service…
```

---

# Appendix D - Hardware use-case that should guide design

A user owns an inexpensive 8-fader MIDI controller.

Desired setup:

```text
1 Current Game
2 Communications
3 Music
4 Browser
5 Media
6 System Sounds
7 Microphone
8 Master
```

They start Le Mans Ultimate.

Fader 1 controls it.

They close LMU and later start Euro Truck Simulator 2.

Fader 1 automatically controls ETS2 because its target is the semantic role `CurrentGame`, not a transient PipeWire node.

That workflow is a product-level acceptance test, not merely a future demo.

---

# Appendix E - Things to revisit before public 1.0

- final name/trademark/package ID;
- Avalonia exact version;
- .NET exact SDK;
- native Wayland qualification;
- Flatpak controller device access;
- supported minimum PipeWire version;
- exact Steam metadata strategy;
- whether group faders use absolute or multiplicative behavior;
- microphone inclusion in 1.0;
- license;
- initial distro support statement;
- official tested MIDI devices.
