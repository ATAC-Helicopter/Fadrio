# Debugging PipeWire

Useful comparison tools include:

```bash
wpctl status
pw-cli ls Node
pw-dump
```

These commands are for diagnostics only and must not become production backend behavior.

Use `dotnet run --project src/Fadrio.Cli -- apps --watch` to inspect Fadrio's normalized logical view. Do not publish complete PipeWire dumps without reviewing application names, media titles, PIDs, and other private metadata.
