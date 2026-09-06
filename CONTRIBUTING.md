# Contributing to Fadrio

Thank you for helping build Fadrio. The project is early, so focused changes with clear evidence are especially valuable.

## Before changing code

1. Read `TECH_SPEC.md` and `AGENTS.md`.
2. Check the roadmap and existing issues.
3. Discuss changes that alter architecture, persistent formats, native ABI, product scope, or UX contracts before implementing them.
4. Create an ADR for an intentional material departure from the specification.

## Development workflow

Use branches such as `feature/application-resolver`, `fix/native-lifecycle`, or `docs/build-guide`. Keep unrelated formatting out of functional changes.

Before opening a pull request:

```bash
./scripts/build.sh
./scripts/test.sh
```

Resolver changes require synthetic fixtures. Native changes require ownership/lifetime review and native tests. UI changes require a keyboard/accessibility note and screenshots when visible.

## Commit and pull-request guidance

Clear prefixes are encouraged: `feat:`, `fix:`, `native:`, `ui:`, `docs:`, `test:`, and `build:`. A pull request should explain motivation, behavior, testing, privacy/security impact, accessibility impact, and any packaging implications.

By contributing, you agree that your contribution is licensed under GPL-3.0-or-later, the same license as the project.
