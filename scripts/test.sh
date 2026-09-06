#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$repository_root/scripts/build.sh"
ctest --test-dir "$repository_root/build/native" --output-on-failure
dotnet test "$repository_root/Fadrio.slnx" --configuration Debug --no-build

if [[ "${FADRIO_RUN_PIPEWIRE_INTEGRATION:-0}" == "1" ]]; then
    "$repository_root/scripts/test-pipewire-integration.sh"
fi
