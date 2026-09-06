# Fadrio roadmap

This is the canonical product and delivery roadmap for Fadrio. GitHub issues, milestones, pull requests, project items, and changelog entries mirror this file; they do not define a competing scope.

## Work-item protocol

- Product and engineering work uses `FAD-xxxx`.
- Defects use `BUG-xxxxx`.
- Release and qualification gates use `REL-xxxxx`.
- IDs are allocated sequentially within their namespace, never reused, and never renumbered when plans move.
- Each execution item has exactly one owning ID.
- `P0` is release-blocking reliability, security, privacy, or accessibility work; `P1` is primary milestone value; `P2` is valuable deferrable work.
- Roadmap checkboxes are canonical delivery state. GitHub status, milestone, labels, and project fields must agree.
- Active execution is declared with a nested `Status: In progress` line and mirrored to the GitHub Project; incomplete tickets without it remain Todo.
- A changelog entry representing user-visible or release-scope work includes its owning ID.
- Scope and acceptance text remain in this roadmap. GitHub issue bodies may add implementation notes without weakening this contract.

Canonical ticket format:

```text
- [ ] `FAD-0001` `P1` Clear one-line scope.
  - Scope: Exact implementation boundary.
  - Acceptance: Observable completion condition.
```

## Milestone sequence

| Milestone | State | Outcome |
|---|---|---|
| `M0` | Complete | Native PipeWire, identity, aggregation, managed state, CLI, and repository foundation |
| `M0.2` | Complete | Real-application identity and isolated reconnect qualification |
| `0.2` | Active | Hardened application identity and icon resolution |
| `0.3` | Planned | Durable application preferences and stable mixer behavior |
| `0.4` | Planned | Steam, Proton, and Wine identity |
| `0.5` | Planned | Accessible production mixer and tray UI |
| `0.6` | Planned | ALSA MIDI, Learn, bindings, and pickup mode |
| `0.7` | Planned | Groups and manual profiles |
| `0.8` | Planned | Reliability, diagnostics, and packaging qualification |
| `0.9` | Candidate | Public beta readiness |
| `1.0` | Candidate | First stable release |

---

## M0 — PipeWire application identity spike

**Status:** Complete and published.  
**Tag:** `v0.1.0-m0`

- [x] `FAD-0001` `P0` Establish the repository, solution, architecture rules, and open-source governance.
  - Scope: .NET solution, project boundaries, build policy, documentation, GPL license, contribution rules, and CI.
  - Acceptance: repository builds with warnings as errors, documentation states current limitations, and governance files are present.
- [x] `FAD-0002` `P0` Define the application-centric core domain.
  - Scope: stable identifiers, identity evidence/confidence, audio sessions, runtime applications, mixer targets, and immutable snapshots.
  - Acceptance: Core has no platform dependencies and PID never participates in persistent application identity.
- [x] `FAD-0003` `P0` Implement the versioned native PipeWire C bridge.
  - Scope: context lifecycle, registry playback-node discovery, metadata, volume, mute, event callback, and explicit ownership.
  - Acceptance: the bridge compiles with strict warnings, connects to the user PipeWire instance, observes add/remove/change events, and passes lifecycle tests.
- [x] `FAD-0004` `P0` Implement managed native interop and serialized backend events.
  - Scope: ABI validation, P/Invoke lifecycle, immediate callback copying, normalized records, and channel delivery.
  - Acceptance: unmanaged pointers remain inside the boundary and fake native events are testable without PipeWire.
- [x] `FAD-0005` `P1` Implement Linux process metadata and cached XDG desktop identity.
  - Scope: race-safe `/proc` access, desktop parsing, `Exec` normalization, indexing, evidence, confidence, and stable fallbacks.
  - Acceptance: native, browser, missing process, disappearing process, ambiguous desktop, and executable fallback fixtures pass.
- [x] `FAD-0006` `P0` Aggregate sessions into logical applications through one state coordinator.
  - Scope: serialized add/change/remove handling, canonical grouping, generation isolation, immutable snapshots, and command fan-out.
  - Acceptance: multiple same-identity sessions form one application and application commands affect only its sessions.
- [x] `FAD-0007` `P1` Provide M0 application diagnostics and control through `fadrioctl`.
  - Scope: list/watch applications and diagnostic volume/mute commands.
  - Acceptance: output shows canonical identity, confidence, sessions, runtime PIDs, volume, mute, and icon without persisting runtime data.
- [x] `FAD-0008` `P1` Establish the minimal Avalonia application shell.
  - Scope: narrow vertical empty state, view-model separation, and system/light/dark theme infrastructure.
  - Acceptance: compiled XAML builds without fake production data or business logic in code-behind.
- [x] `FAD-0009` `P0` Add repeatable managed/native build and test automation.
  - Scope: scripts, fixtures, GitHub Actions, central packages, formatting, and dependency direction checks.
  - Acceptance: all managed tests and native lifecycle tests pass in a prepared Linux environment.
- [x] `FAD-0010` `P0` Qualify multi-session control and managed reconnect generations.
  - Scope: two controlled silent playback streams, application-wide set/mute, unrelated-session isolation, and reconnect backoff.
  - Acceptance: both owned streams change together, unrelated audio remains untouched, and reconnect produces a new generation.
- [x] `REL-00001` `P0` Record the M0 evidence baseline.
  - Scope: build, unit-test, native lifecycle, live connection, grouping, volume, and mute results.
  - Acceptance: evidence is documented without claiming unperformed Firefox, Discord, Proton, or PipeWire-restart qualification.

---

## M0.2 — Real application and reconnect qualification

**Status:** Complete and published.

**Tag:** `v0.2.0-alpha.1`

- [x] `FAD-0011` `P0` Build an isolated PipeWire integration-test harness.
  - Scope: controlled daemon/socket, silent streams, deterministic teardown, and no mutation of unrelated user sessions.
  - Acceptance: CI-capable tests cover connect, add, change, remove, volume, mute, and reconnect without physical hardware.
- [x] `FAD-0012` `P1` Qualify browser multi-stream identity with Brave.
  - Scope: multiple Brave playback streams, launcher-wrapper `/proc`/desktop evidence, grouping, command fan-out, and stream recreation.
  - Acceptance: Brave appears once with a stable canonical XDG identity and every owned session follows commands.
- [x] `FAD-0013` `P1` Qualify Discord or equivalent Electron application identity.
  - Scope: renderer/process ambiguity, desktop corroboration, grouping, and diagnostics.
  - Acceptance: the user-facing identity is the installed application, not Electron or a renderer helper.
- [x] `FAD-0014` `P0` Add the first Steam/Proton identity fixture and qualification path.
  - Scope: synthetic process/compat metadata, resolver seam implementation, stable Steam ID, and one real-game comparison when available.
  - Acceptance: a Proton audio stream resolves to `steam:<app-id>` without using Wine loader or PID as identity.
- [x] `FAD-0015` `P0` Qualify PipeWire restart and reconnect end to end.
  - Scope: disconnect signal, unavailable snapshot, bounded backoff, registry rebuild, stale-event rejection, and command recovery.
  - Acceptance: an isolated PipeWire restart produces no duplicate applications and resumes correct control.
- [x] `FAD-0016` `P1` Harden native ownership and sanitizer coverage.
  - Scope: ASAN/UBSAN test job, callback lifetime stress, rapid node churn, and shutdown races.
  - Acceptance: native tests are clean under sanitizers with deterministic lifecycle behavior.
- [x] `FAD-0017` `P1` Enforce protected mainline collaboration on GitHub.
  - Scope: protect `main` from deletion and force-pushes, require pull requests and the strict `linux` status check, and preserve an auditable repository policy.
  - Acceptance: an active GitHub ruleset enforces the documented policy; any hosting-plan or visibility dependency remains explicitly tracked until resolved.
- [x] `BUG-00001` `P0` Keep roadmap synchronization working after a milestone closes.
  - Scope: update managed issues through the GitHub REST API using milestone numbers instead of CLI lookup by open-milestone title.
  - Acceptance: a full apply can update issues assigned to closed `M0` and synchronize every project item without error.
- [x] `BUG-00002` `P0` Flush native stream commands before short-lived client teardown.
  - Scope: wait for a PipeWire core round trip after successful volume and mute parameter requests.
  - Acceptance: isolated integration clients can exit immediately after a successful command without dropping that command.
- [x] `FAD-0018` `P1` Establish the first public Fadrio branding and project presence.
  - Scope: curate theme-aware repository wordmarks, preserve descriptive asset names, and publish an honest FG Labs project page linked to source and roadmap.
  - Acceptance: repository and website builds use the selected assets, describe the current early-development state accurately, and expose no generated filenames publicly.
- [x] `FAD-0019` `P1` Prepare the public repository and source-only alpha release path.
  - Scope: public metadata, security settings, contributor-facing setup validation, tag-driven source releases, checksums, release notes, and FG Labs release visibility.
  - Acceptance: the public repository has protected mainline collaboration, a reproducible prerelease workflow, current documentation, and a discoverable source-only alpha release with no unsupported binary claims.
- [x] `REL-00002` `P0` Close the M0.2 qualification gate.
  - Scope: collect evidence for Brave/browser, Electron, Proton fixture, restart, sanitizers, and current limitations.
  - Acceptance: M0.2 evidence is reproducible and no unresolved P0 item remains.

---

## 0.2 — Application identity

**Latest snapshot:** `v0.2.0-alpha.2`
**Tag:** `v0.2.0-alpha.2`

- [x] `FAD-0020` `P1` Adopt Fadrio as the complete product identity.
  - Scope: rename user-facing and internal application identity, managed projects and namespaces, executables, native ABI, XDG paths, repository/tracker metadata, release artifacts, and website; replace the previous artwork with the supplied Fadrio branding package.
  - Acceptance: tracked source and current public surfaces use Fadrio consistently, the renamed solution passes the full managed/native test pipeline, and the Fadrio alpha release and FG Labs page are live.
- [x] `FAD-0201` `P0` Replace linear resolver decisions with deterministic multi-evidence scoring and conflict handling.
- [ ] `FAD-0202` `P1` Implement secure local icon resolution and bounded caching.
- [ ] `FAD-0203` `P1` Add Flatpak-first canonical identity and fixtures.
- [ ] `FAD-0204` `P2` Add Snap wrapper identity without making Snap a dependency.
- [ ] `FAD-0205` `P1` Add resolver cache invalidation and desktop-index refresh watching.
- [ ] `FAD-0206` `P1` Add inspectable advanced identity/session diagnostics.
- [ ] `REL-00003` `P0` Qualify one-row Firefox identity, icons, ambiguity safety, and fallback behavior.

## 0.3 — Persistence

- [ ] `FAD-0301` `P0` Implement XDG data paths, SQLite bootstrap, and deterministic migrations.
- [ ] `FAD-0302` `P1` Persist application identity evidence and user overrides without raw sensitive process context.
- [ ] `FAD-0303` `P1` Implement pin, hide, ordering, and inactive-stream grace behavior.
- [ ] `FAD-0304` `P0` Implement remembered/fixed/follow-current volume policies with confidence safeguards.
- [ ] `FAD-0305` `P0` Add migration backups, corruption failure behavior, and upgrade tests.
- [ ] `REL-00004` `P0` Qualify durable preferences across application and audio-service restarts.

## 0.4 — Steam, Proton, and Wine

- [ ] `FAD-0401` `P0` Discover and cache Steam libraries and app manifests.
- [ ] `FAD-0402` `P0` Resolve Proton process chains and compat paths to stable Steam identities.
- [ ] `FAD-0403` `P1` Resolve non-Steam Wine prefixes and target executables safely.
- [ ] `FAD-0404` `P1` Resolve locally installed game icons without redistributing artwork.
- [ ] `FAD-0405` `P1` Prototype editable `CurrentGame` classification.
- [ ] `REL-00005` `P0` Qualify native Steam, Proton, and non-Steam Wine scenarios.

## 0.5 — UI alpha

- [ ] `FAD-0501` `P0` Connect immutable mixer snapshots and commands to the Avalonia view models.
- [ ] `FAD-0502` `P1` Build accessible output and logical-application rows with mixed-volume state.
- [ ] `FAD-0503` `P1` Add stable sorting, interaction freeze, empty, unavailable, and reconnect states.
- [ ] `FAD-0504` `P1` Implement tray popup, single-instance activation, and clean shutdown.
- [ ] `FAD-0505` `P0` Establish localization resources, keyboard navigation, screen-reader labels, and scaling tests.
- [ ] `REL-00006` `P0` Qualify the mixer on GNOME/KDE Wayland and X11 at supported scale factors.

## 0.6 — MIDI controllers

- [ ] `FAD-0601` `P0` Define controller devices, controls, bindings, targets, origins, and feedback contracts.
- [ ] `FAD-0602` `P1` Implement deterministic fake-controller tests and pickup mode.
- [ ] `FAD-0603` `P0` Implement ALSA Sequencer enumeration, hotplug, input, and optional output.
- [ ] `FAD-0604` `P1` Build accessible MIDI Learn and binding management.
- [ ] `FAD-0605` `P0` Add reconnect-safe matching and feedback loop suppression.
- [ ] `REL-00007` `P0` Qualify an absolute fader, rotary controller, and buttons without physical-position jumps.

## 0.7 — Profiles and groups

- [ ] `FAD-0701` `P1` Implement organizational application groups with an explicitly documented volume rule.
- [ ] `FAD-0702` `P1` Implement manual profiles and profile-specific controller mappings.
- [ ] `FAD-0703` `P1` Implement editable semantic roles for Communications, Music, Browser, System Sounds, and Current Game.
- [ ] `REL-00008` `P0` Qualify predictable profile, group, and dynamic-role control behavior.

## 0.8 — Reliability and packaging

- [ ] `FAD-0801` `P0` Complete PipeWire/controller restart, churn, and long-running stability tests.
- [ ] `FAD-0802` `P0` Add privacy-redacted diagnostics export and bounded local logging.
- [ ] `FAD-0803` `P0` Build and smoke-test portable tarball and Flatpak packages.
- [ ] `FAD-0804` `P1` Add Debian packaging and installation/removal tests.
- [ ] `FAD-0805` `P1` Measure idle CPU, memory, startup, and meter update budgets.
- [ ] `REL-00009` `P0` Close the cross-distribution reliability and packaging matrix.

## 0.9 — Public beta candidate

- [ ] `FAD-0901` `P0` Complete onboarding, user documentation, diagnostics, and support workflows.
- [ ] `FAD-0902` `P0` Complete accessibility and fractional-scaling qualification.
- [ ] `FAD-0903` `P1` Complete hardware qualification and controller documentation.
- [ ] `FAD-0904` `P0` Complete dependency, native-memory, privacy, and package security review.
- [ ] `REL-00010` `P0` Approve or reject public beta publication from collected evidence.

## 1.0 — Stable candidate

- [ ] `FAD-1001` `P0` Confirm final product name, application ID, package identity, and supported-platform statement.
- [ ] `FAD-1002` `P0` Freeze and document persistent, native ABI, D-Bus, and export format versions.
- [ ] `FAD-1003` `P0` Complete the release qualification scenarios in `TECH_SPEC.md`.
- [ ] `FAD-1004` `P0` Produce reproducible signed release artifacts, checksums, and SBOMs.
- [ ] `REL-00011` `P0` Approve the first stable release only when every 1.0 contract is evidenced.

## Identifier ledger

- Next `FAD-00xx`: `FAD-0021`
- Next `FAD-02xx`: `FAD-0207`
- Next `FAD-03xx`: `FAD-0306`
- Next `FAD-04xx`: `FAD-0406`
- Next `FAD-05xx`: `FAD-0506`
- Next `FAD-06xx`: `FAD-0606`
- Next `FAD-07xx`: `FAD-0704`
- Next `FAD-08xx`: `FAD-0806`
- Next `FAD-09xx`: `FAD-0905`
- Next `FAD-10xx`: `FAD-1005`
- Next `BUG`: `BUG-00003`
- Next `REL`: `REL-00012`
