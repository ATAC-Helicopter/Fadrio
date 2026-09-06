#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

for command in cmake pkg-config; do
    if ! command -v "$command" >/dev/null 2>&1; then
        echo "Missing build tool: $command. See docs/development/setup-linux.md." >&2
        exit 1
    fi
done

missing=0
for package in libpipewire-0.3 libspa-0.2 alsa; do
    if ! pkg-config --exists "$package"; then
        echo "Missing native development package: $package" >&2
        missing=1
    fi
done
if [[ "$missing" -ne 0 ]]; then
    echo "Install the dependencies listed in docs/development/setup-linux.md." >&2
    exit 1
fi

cmake -S "$repository_root/src/native/fadrio-native" -B "$repository_root/build/native" -DCMAKE_BUILD_TYPE=Debug
cmake --build "$repository_root/build/native" --parallel
