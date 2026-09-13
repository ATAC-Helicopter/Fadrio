# Electron identity qualification

`FAD-0013` uses installed Visual Studio Code as the Electron application equivalent to Discord. The qualification covers application identity; it does not claim Discord Flatpak qualification.

On 2026-09-05, a temporary development extension in an isolated VS Code profile enabled the built-in terminal-bell audio signal at 1% signal volume and generated a bounded sequence of terminal bells. PipeWire reported `Chromium` as the application name, `code` as the binary, and `chromium-browser` as the icon. The real audio-service executable resolved to `/usr/share/code/code`.

After resolving the installed `code.desktop` alongside its hidden URL handler, the CLI reported one `Visual Studio Code` application, canonical ID `xdg:code`, High confidence, one session, and icon `vscode`. The ordinary IDE profile was not used for the fixture.

Synthetic desktop fixtures reproduce the main entry and `NoDisplay=true` URL handler. Resolver tests check the generic Chromium metadata, executable evidence, name/icon correction, and two-session grouping. Hidden helpers are collapsed only when they share the visible entry's exact absolute executable and nonempty icon. Other desktop ambiguity remains unresolved.

To reproduce the real observation, use a separate VS Code user-data and extension directory, enable `accessibility.signals.terminalBell.sound`, set `accessibility.signalOptions.volume` low, and run a bounded terminal bell loop. Observe with `fadrioctl apps`, then close the temporary IDE instance. Record identity and confidence only; omit PIDs, private workspace paths, and terminal history.
