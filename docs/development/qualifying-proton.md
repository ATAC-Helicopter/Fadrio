# Steam and Proton qualification

`FAD-0014` introduces the first production Steam resolver and fixture qualification path.

The resolver reads a bounded allowlist of identity environment keys only for Wine or Windows executable processes. It discovers Steam libraries from local library metadata and caches installed manifests. It requires numeric application IDs to agree with one indexed library's compatibility directory and installed game manifest before returning `steam:<app-id>`. Missing, malformed, contradictory, or inaccessible evidence falls back to ordinary executable/application resolution.

Fixture tests cover external-library discovery, manifest naming, canonical identity and display name, disagreement between environment IDs, invalid IDs, compatibility-directory mismatch, malformed manifests, and missing process environment. No user environment or proprietary binary is included.

During development on 2026-09-05, the diagnostic CLI observed a running Proton instance of Euro Truck Simulator 2 as one `Euro Truck Simulator 2` application with canonical ID `steam:227300`, High confidence, and 21 sessions. The game was already running; this observation was read-only. No game control, icon, controller mapping, or saved-preference qualification is claimed by this observation.

To reproduce, build the CLI, run an installed Proton game with audio, and use `fadrioctl apps`. Confirm the canonical ID and name against its installed Steam manifest. Record only the application name, canonical ID, confidence, session count, and platform versions. Do not commit PIDs, account data, environments, or full command lines.

The initial index is rebuilt at process startup. Automatic index refresh, arbitrary prefix locations, native Steam games, non-Steam Wine, game artwork, and controller role mapping remain later milestone work.
