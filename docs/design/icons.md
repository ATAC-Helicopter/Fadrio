# Icons

Icon resolution order is user override, desktop entry, PipeWire icon name, local Steam/application artwork, category fallback, then generic application icon.

Bundled icons require editable source and known licensing. Runtime-resolved installed icons are not automatically redistributable repository assets. Cap file size and dimensions, allow only supported local formats, and never download icons from the network in v1.

The current local resolver accepts PNG files only. It searches configured XDG icon roots with a bounded, deterministic traversal, rejects URLs and paths outside those trusted roots (including symlink escapes), and validates the PNG signature and dimensions before copying it. Cached files live under `$XDG_CACHE_HOME/fadrio/icons/`, are keyed by source path plus modification signature, and are pruned by both entry count and total bytes. Vector decoding and rasterization remain out of scope until they can use a reviewed decoder.
