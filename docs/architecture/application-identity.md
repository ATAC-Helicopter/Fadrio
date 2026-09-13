# Application identity

The resolver returns an `ApplicationIdentity`, confidence, and inspectable evidence. Persistent canonical IDs never contain PID.

The resolver evaluates these evidence sources together:

1. PipeWire application/process metadata;
2. `/proc/<pid>/exe` when available;
3. executable basename/path;
4. PipeWire application and icon identifiers;
5. trusted Flatpak sandbox metadata from `/proc/<pid>/root/.flatpak-info`;
6. one cached XDG desktop-entry index;
7. a stable hash of the best non-PID fallback evidence.

Examples include `xdg:org.mozilla.firefox`, `exe:/usr/bin/vlc`, and `unknown:<hash>`. Ambiguous desktop matches reduce confidence instead of guessing.

Flatpak identity is evaluated before Steam and ordinary desktop scoring. A syntactically valid application ID read from the process sandbox produces `flatpak:<app-id>`; an exported host desktop entry with the same ID supplies its display name and icon. Fadrio reads only the bounded `[Application] name` value and does not require a portal or elevated access. Missing, inaccessible, oversized, or invalid metadata falls through to the ordinary resolver.

Snap identity is secondary and has no runtime dependency on the `snap` command. The process metadata provider retains only `SNAP_NAME`, `SNAP_INSTANCE_NAME`, and `SNAP`; resolution requires a valid package name and agreement with `/snap/<name>/`. Exported `X-SnapInstanceName` desktop metadata supplies presentation details. Contradictory or incomplete evidence falls through instead of guessing.

Desktop candidates receive fixed weights for exact executable paths, executable basenames, application IDs, and icon IDs. Scores are accumulated by canonical desktop ID and candidates are ordered deterministically. Agreeing independent evidence raises confidence. When two strong candidates remain too close, the resolver records `ConflictingEvidence` and falls back to the stable executable or PipeWire identity instead of choosing a desktop application. Scores and rejected conflicts remain inspectable in the evidence list.

M0.2 implements the Steam resolver seam. Relevant Wine/Windows processes supply a bounded allowlist of environment evidence. A cached index discovers libraries through `libraryfolders.vdf` and reads installed app manifests. Agreeing numeric Steam IDs, an indexed compatibility directory, and an installed manifest produce `steam:<app-id>` at High confidence. Conflicts or missing evidence fall back to the ordinary resolver. The index refreshes when the CLI is restarted; automatic refresh and broader Wine/native Steam coverage remain in milestone 0.4.

Resolver changes require synthetic fixture tests and must preserve existing user mappings once persistence exists.
