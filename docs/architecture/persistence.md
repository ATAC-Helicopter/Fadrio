# Persistence foundation

`FAD-0301` establishes per-user paths and a SQLite schema bootstrap. The desktop app initializes the database before creating its window. The CLI does not create a database just to inspect live audio.

The config, data, cache, and state directories use absolute `XDG_*_HOME` values with standard home-directory fallbacks. Structured data is stored at `$XDG_DATA_HOME/fadrio/fadrio.db` (or `~/.local/share/fadrio/fadrio.db`). A newly created Fadrio data directory is private to the current user.

Migration 1 creates `settings` and records its version, name, and SQL checksum in `schema_migrations`. Startup verifies SQLite integrity and the migration history before applying missing migrations in a transaction. Unknown future versions, changed checksums, and failed SQL stop startup without resetting user data. Later persistence work adds application records and migration backups; the initial database has no user identity data yet.

Live meter samples and transient runtime sessions do not belong in the database. Resolver changes must never silently delete identities, mappings, bindings, or user personalization. Each later schema change needs a numbered transactional migration and an upgrade test from the previous release schema.
