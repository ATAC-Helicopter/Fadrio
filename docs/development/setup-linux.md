# Linux development setup

Fadrio requires the .NET 10 SDK, a C11 compiler, CMake, pkg-config, PipeWire/SPA development headers, and ALSA development headers.

On Debian and Ubuntu family systems:

```bash
sudo apt install \
  build-essential \
  cmake \
  ninja-build \
  pkg-config \
  libpipewire-0.3-dev \
  libspa-0.2-dev \
  libasound2-dev
```

Install the .NET 10 SDK using the supported Microsoft or distribution instructions. The repository pins SDK feature band 10.0.300 and rolls forward to a newer patch in that band.

Validate an existing environment without changing it:

```bash
./scripts/bootstrap-linux.sh
```

On Debian/Ubuntu, install missing native packages and then validate with:

```bash
./scripts/bootstrap-linux.sh --install
```

The app connects to the current user's PipeWire instance. It does not require root or a system service.
