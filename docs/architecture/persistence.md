# Persistence foundation

`FAD-0301` establishes per-user paths and a SQLite schema bootstrap. The desktop app initializes the database before creating its window. The CLI does not create a database just to inspect live audio.

The config, data, cache, and state directories use absolute `XDG_*_HOME` values with standard home-directory fallbacks. Structured data is stored at `$XDG_DATA_HOME/fadrio/fadrio.db` (or `~/.local/share/fadrio/fadrio.db`). A newly created Fadrio data directory is private to the current user.

Migration 1 creates `settings` and records its version, name, and SQL checksum in `schema_migrations`. Startup verifies SQLite integrity and the migration history before applying missing migrations in a transaction. Unknown future versions, changed checksums, and failed SQL stop startup without resetting user data. Migration backups remain a later persistence milestone.

Migration 2 adds `applications` and `application_evidence`. Stable XDG, Flatpak, Snap, and Steam identities may store their resolved name, confidence, and allowlisted desktop/sandbox identifiers. Process paths, PIDs, media names, environment values, and arbitrary resolver descriptions are never written as evidence. Fallback identities such as `exe:/path` use a deterministic opaque key for explicit user overrides; their resolved names and evidence are not stored. Name/icon overrides survive resolver metadata refresh and are applied when the CLI resolves a live application.

Live meter samples and transient runtime sessions do not belong in the database. Resolver changes must never silently delete identities, mappings, bindings, or user personalization. Each later schema change needs a numbered transactional migration and an upgrade test from the previous release schema.
