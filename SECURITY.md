# Security policy

## Supported versions

Fadrio is currently pre-release. Security fixes are made on the latest `main` branch and may be published in a newer alpha. Alpha snapshots are provided for evaluation, not as a supported production release line.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Use GitHub private vulnerability reporting when enabled, or contact a repository maintainer privately through their published project profile.

Include the affected revision, impact, reproduction steps, and relevant redacted diagnostics. Never include full environment dumps, private command lines, credentials, controller serial numbers, or unrelated application history.

Relevant areas include native memory safety, malicious desktop files, D-Bus authorization, unsafe path handling, configuration imports, icon parsing, and diagnostic-data disclosure.
