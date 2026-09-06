#!/usr/bin/env bash
set -euo pipefail

native_packages=(
    build-essential
    cmake
    ninja-build
    pkg-config
    libpipewire-0.3-dev
    libspa-0.2-dev
    libasound2-dev
)

if [[ "${1:-}" == "--install" ]]; then
    sudo apt-get update
    sudo apt-get install -y "${native_packages[@]}"
elif [[ $# -ne 0 ]]; then
    echo "Usage: $0 [--install]" >&2
    exit 2
fi

missing=0
for command in dotnet cmake ninja pkg-config gcc; do
    if ! command -v "$command" >/dev/null 2>&1; then
        echo "Missing tool: $command" >&2
        missing=1
    fi
done

if command -v dotnet >/dev/null 2>&1 && [[ "$(dotnet --version)" != 10.* ]]; then
    echo "Fadrio requires the .NET 10 SDK; found $(dotnet --version)." >&2
    missing=1
fi

if command -v pkg-config >/dev/null 2>&1; then
    for package in libpipewire-0.3 libspa-0.2 alsa; do
        if ! pkg-config --exists "$package"; then
            echo "Missing development package: $package" >&2
            missing=1
        fi
    done
fi

if [[ $missing -ne 0 ]]; then
    echo "Install the prerequisites in docs/development/setup-linux.md or run $0 --install for native packages." >&2
    exit 1
fi

echo "Fadrio development prerequisites are ready."
