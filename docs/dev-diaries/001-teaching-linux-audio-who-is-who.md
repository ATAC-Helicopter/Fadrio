# Fadrio Dev Diary #1 — Teaching Linux audio who is who

> Suggested Reddit flair: `Development Update`  
> Suggested community: `r/Fadrio`  
> Status: ready to publish  
> Canonical article: <https://fglabs.dev/devlog/fadrio-dev-diary-01-teaching-linux-audio-who-is-who>  
> Cover image: <https://fglabs.dev/products/fadrio/dev-diary-01-identity-workshop.png>  
> Source asset: `FGLabs-site/public/products/fadrio/dev-diary-01-identity-workshop.png`

Fadrio is supposed to be the simple Linux mixer: Firefox gets one slider, a game gets one slider, Discord gets one slider, and nobody is asked to memorize PipeWire node 81.

The awkward part is that Linux audio does not hand us a neat card saying, “Hello, I am definitely Firefox and all six of these streams belong to me.” It hands us processes, wrappers, sandboxes, helper binaries, metadata of varying enthusiasm, and occasionally something that disappears halfway through being inspected.

The current development rule is simple:

> If Fadrio cannot explain why it identified an application, it is not allowed to guess confidently.

## The identity problem hiding underneath the volume slider

A modern application can create several playback streams. A browser may have separate renderers. Electron applications bring helpers. Steam launches Proton, Proton launches Windows executables, and the audio stream may introduce itself with the software equivalent of “Dave.”

Showing every stream directly would technically work, in the same way that emptying a box of assorted screws onto the floor technically counts as inventory management.

Fadrio instead builds an evidence-based application identity. PipeWire metadata, process information, executable paths, XDG desktop entries, sandbox metadata, and stable fallbacks contribute evidence. Strong agreement produces a recognizable application. Conflicting evidence lowers confidence and falls back safely instead of assigning the wrong icon and name.

## What changed during the 0.2 identity milestone

- Desktop candidates now use deterministic multi-evidence scoring rather than a chain of first-match guesses.
- Local icons are resolved only from trusted XDG roots, with traversal, symlink escape, malformed image, excessive dimension, and cache-size protections.
- Flatpak identity comes from bounded sandbox metadata and can be enriched with the exported host desktop entry.
- Snap identity uses only a small allowlist of process variables and requires the package name to agree with its mounted `/snap` root. Fadrio does not invoke Snap tooling.
- The desktop-entry index now refreshes when applications are installed, updated, renamed, or removed while Fadrio is running.

## The filesystem watcher that became a state-management problem

Watching a directory is easy. Making the result safe for a live mixer is the actual work.

Desktop updates often arrive as bursts of create, write, rename, and delete notifications. Rebuilding on every notification wastes work and can briefly expose half-written state. Fadrio now debounces those bursts, builds a complete replacement index, and atomically publishes the new snapshot with a monotonic revision number.

The mixer coordinator notices the revision on its serialized event path and resolves each active session once. Filesystem callbacks never reach into UI state. If a refresh fails, the previous identities stay intact and the revision is retried later. Disposal is synchronized so a timer callback cannot wake up after shutdown and perform one final, unsolicited encore.

### The evidence chain, in one glance

| Layer | What it contributes | What Fadrio refuses to do |
| --- | --- | --- |
| PipeWire | Application and media hints | Treat a transient node ID as identity |
| Process metadata | Executable and bounded environment evidence | Persist a PID as identity |
| XDG / sandbox metadata | Stable name, desktop ID, and icon | Trust an uncorroborated wrapper |
| Resolver | Confidence and inspectable evidence | Guess through a strong conflict |

## How much testing does a directory watcher need?

Apparently: yes.

The focused coverage exercises create, change, delete, refresh visibility, burst debouncing, disposal with a queued timer, revision initialization, exactly-once re-resolution, and recovery after a resolver failure.

The full local gate passed:

- 69 managed tests;
- the native lifecycle test;
- six roadmap tooling tests;
- formatting verification;
- roadmap validation;
- clean-diff checks.

PR #97 then passed both the Linux and native-sanitizer checks and merged. That distinction matters: local green lights are evidence, but they are not a tiny CI costume pretending to be a shipped change.

## And the UI? Yes, there will be a UI.

Fadrio already has a minimal Avalonia shell, but the finished mixer is not here yet. The current roadmap places the production mixer in milestone 0.5, after persistence and broader Steam, Proton, and Wine work.

I do not want visual polish to arrive so late that the underlying interaction model cannot benefit from real feedback, so the likely move is to finish the last two 0.2 items and bring the first functional mixer slices forward.

Next are inspectable advanced diagnostics and the final Firefox identity qualification. Then we can connect immutable mixer snapshots to real view models and start replacing architecture diagrams with screenshots.

Until then, the penguin is wearing a hard hat because the UI is, quite literally, still a construction site.

## A small publishing experiment

I plan to publish two development diaries most weeks:

1. one deep technical entry like this one;
2. one shorter visual progress report, qualification note, or design decision.

The full entries will live on [fglabs.dev](https://fglabs.dev/devlog) and be mirrored to `r/Fadrio`, so the archive remains readable even when a feed is unavailable. This diary already has a [clean, rich-text web version](https://fglabs.dev/devlog/fadrio-dev-diary-01-teaching-linux-audio-who-is-who) with the evidence table and cover art intact.

Questions and disagreement are welcome—especially from people who have wrestled with PipeWire application identity in the wild. Which Linux applications have produced the strangest names or grouping behavior on your system?
