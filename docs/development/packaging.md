# Packaging development

Packaging starts only after the backend and identity spike is reliable. The planned order is portable tarball, Flatpak, Debian package, AppImage, then RPM.

Release artifacts must include the managed executables, `libfadrio_native.so`, desktop integration, original license, third-party notices, and checksums. Package removal and user-data purge are separate operations.
