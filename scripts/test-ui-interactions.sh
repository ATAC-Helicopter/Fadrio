#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"
for command in xwininfo xprop ffmpeg pipewire pw-cat dotnet rg python3; do
    command -v "$command" >/dev/null 2>&1 || { echo "Missing $command" >&2; exit 77; }
done
[[ -n "${DISPLAY:-}" ]] || { echo 'An X11/XWayland display is required.' >&2; exit 77; }
if xwininfo -root -tree | rg '"Fadrio"' >/dev/null; then
    echo 'Close the existing Fadrio test window before running.' >&2
    exit 1
fi
fadrio_fixture_dir=$(mktemp -d -t fadrio-gesture-XXXXXX)
fadrio_fixture_pipewire_pid=""
fadrio_fixture_stream_pid=""
fadrio_fixture_ui_pid=""
cleanup() {
    for fadrio_fixture_pid in "$fadrio_fixture_ui_pid" "$fadrio_fixture_stream_pid" "$fadrio_fixture_pipewire_pid"; do
        if [[ -n "$fadrio_fixture_pid" ]]; then kill "$fadrio_fixture_pid" 2>/dev/null || true; fi
    done
    wait 2>/dev/null || true
    printf 'fixture_dir=%s\n' "$fadrio_fixture_dir"
}
trap cleanup EXIT INT TERM
export XDG_RUNTIME_DIR="$fadrio_fixture_dir"
export PIPEWIRE_RUNTIME_DIR="$fadrio_fixture_dir"
export PIPEWIRE_REMOTE=pipewire-0
export XDG_DATA_HOME="$fadrio_fixture_dir/share"
export XDG_CACHE_HOME="$fadrio_fixture_dir/cache"
export FONTCONFIG_FILE="$repository_root/tests/fixtures/fonts/ui-test-fontconfig.conf"
export LD_LIBRARY_PATH="$PWD/build/native"
pipewire -c "$PWD/tests/fixtures/pipewire/fadrio-test.conf" >"$fadrio_fixture_dir/pipewire.log" 2>&1 &
fadrio_fixture_pipewire_pid=$!
for _ in {1..100}; do
    [[ -S "$fadrio_fixture_dir/pipewire-0" ]] && break
    sleep .05
done
[[ -S "$fadrio_fixture_dir/pipewire-0" ]]
pw-cat --playback --target 0 --rate 48000 --channels 2 --format s16 \
    -P '{ application.name = "Fadrio Fixture" application.id = "dev.fglabs.Fadrio.UiFixture" }' \
    - </dev/zero >"$fadrio_fixture_dir/stream.log" 2>&1 &
fadrio_fixture_stream_pid=$!
env -u WAYLAND_DISPLAY dotnet "$PWD/src/Fadrio.UI/bin/Debug/net10.0/fadrio.dll" >"$fadrio_fixture_dir/ui.log" 2>&1 &
fadrio_fixture_ui_pid=$!
fadrio_fixture_window=""
for _ in {1..100}; do
    fadrio_fixture_window=$(xwininfo -root -tree | awk '/"Fadrio"/ && !/mutter/ {if (!found) print $1; found=1}')
    [[ -n "$fadrio_fixture_window" ]] && break
    sleep .1
done
[[ -n "$fadrio_fixture_window" ]]
xprop -id "$fadrio_fixture_window" _NET_WM_PID | rg -F "= $fadrio_fixture_ui_pid" >/dev/null
sleep 1
ffmpeg -hide_banner -loglevel error -f x11grab -window_id "$fadrio_fixture_window" \
    -video_size 360x680 -i "$DISPLAY" -frames:v 1 "$fadrio_fixture_dir/before.png"
python3 "$repository_root/scripts/ui-interaction-probe.py" "$fadrio_fixture_window" "$PWD"
ffmpeg -hide_banner -loglevel error -f x11grab -window_id "$fadrio_fixture_window" \
    -video_size 360x680 -i "$DISPLAY" -frames:v 1 "$fadrio_fixture_dir/mixer.png"
kill -0 "$fadrio_fixture_ui_pid"
python3 "$repository_root/scripts/ui-interaction-probe.py" "$fadrio_fixture_window" "$PWD" "$fadrio_fixture_pipewire_pid"
wait "$fadrio_fixture_pipewire_pid" 2>/dev/null || true
fadrio_fixture_pipewire_pid=""
wait "$fadrio_fixture_stream_pid" 2>/dev/null || true
fadrio_fixture_stream_pid=""
sleep 1
ffmpeg -hide_banner -loglevel error -f x11grab -window_id "$fadrio_fixture_window" \
    -video_size 360x680 -i "$DISPLAY" -frames:v 1 "$fadrio_fixture_dir/reconnecting.png"
pipewire -c "$PWD/tests/fixtures/pipewire/fadrio-test.conf" >>"$fadrio_fixture_dir/pipewire.log" 2>&1 &
fadrio_fixture_pipewire_pid=$!
for _ in {1..100}; do
    [[ -S "$fadrio_fixture_dir/pipewire-0" ]] && break
    sleep .05
done
sleep 3
ffmpeg -hide_banner -loglevel error -f x11grab -window_id "$fadrio_fixture_window" \
    -video_size 360x680 -i "$DISPLAY" -frames:v 1 "$fadrio_fixture_dir/empty.png"
pw-cat --playback --target 0 --rate 48000 --channels 2 --format s16 \
    -P '{ application.name = "Fadrio Fixture" application.id = "dev.fglabs.Fadrio.UiFixture" }' \
    - </dev/zero >>"$fadrio_fixture_dir/stream.log" 2>&1 &
fadrio_fixture_stream_pid=$!
sleep 2
ffmpeg -hide_banner -loglevel error -f x11grab -window_id "$fadrio_fixture_window" \
    -video_size 360x680 -i "$DISPLAY" -frames:v 1 "$fadrio_fixture_dir/recovered-before.png"
python3 "$repository_root/scripts/ui-interaction-probe.py" "$fadrio_fixture_window" "$PWD"
ffmpeg -hide_banner -loglevel error -f x11grab -window_id "$fadrio_fixture_window" \
    -video_size 360x680 -i "$DISPLAY" -frames:v 1 "$fadrio_fixture_dir/recovered.png"
kill -0 "$fadrio_fixture_ui_pid"
