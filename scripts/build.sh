#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$repository_root/scripts/build-native.sh"
dotnet build "$repository_root/Fadrio.slnx" --configuration Debug
