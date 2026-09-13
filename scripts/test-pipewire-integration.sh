#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

for command in pipewire pw-cat dotnet rg; do
    if ! command -v "$command" >/dev/null 2>&1; then
        echo "Skipping isolated PipeWire integration test: missing $command." >&2
        exit 77
    fi
done

runtime_directory="$(mktemp -d -t fadrio-pipewire-XXXXXX)"
pipewire_pid=""
first_stream_pid=""
second_stream_pid=""
watcher_pid=""
churn_pid=""

cleanup() {
    for process_id in "$churn_pid" "$watcher_pid" "$first_stream_pid" "$second_stream_pid" "$pipewire_pid"; do
        if [[ -n "$process_id" ]]; then
            kill "$process_id" 2>/dev/null || true
        fi
    done
    wait 2>/dev/null || true
    rm -rf -- "$runtime_directory"
}
trap cleanup EXIT INT TERM

export XDG_RUNTIME_DIR="$runtime_directory"
export PIPEWIRE_RUNTIME_DIR="$runtime_directory"
export PIPEWIRE_REMOTE="pipewire-0"
export XDG_DATA_HOME="$runtime_directory/share"

mkdir -p "$XDG_DATA_HOME/applications" "$runtime_directory/bin"
cp "$repository_root/tests/fixtures/desktop-files/firefox.desktop" \
    "$XDG_DATA_HOME/applications/firefox.desktop"
firefox_fixture="$runtime_directory/bin/firefox"
cp "$(command -v pw-cat)" "$firefox_fixture"

start_pipewire() {
    local log_path="$1"
    pipewire -c "$repository_root/tests/fixtures/pipewire/fadrio-test.conf" \
        >"$log_path" 2>&1 &
    pipewire_pid=$!

    for _ in {1..100}; do
        if [[ -S "$runtime_directory/pipewire-0" ]]; then
            return
        fi
        if ! kill -0 "$pipewire_pid" 2>/dev/null; then
            echo "Isolated PipeWire exited during startup:" >&2
            sed -n '1,160p' "$log_path" >&2
            exit 1
        fi
        sleep 0.05
    done

    echo "Timed out waiting for the isolated PipeWire socket." >&2
    exit 1
}

wait_for_pattern() {
    local file_path="$1"
    local pattern="$2"
    for _ in {1..150}; do
        if rg -F "$pattern" "$file_path" >/dev/null 2>&1; then
            return
        fi
        sleep 0.1
    done
    echo "Timed out waiting for '$pattern' in $file_path." >&2
    sed -n '1,240p' "$file_path" >&2
    exit 1
}

run_dotnet() {
    if [[ -n "${FADRIO_SANITIZER_PRELOAD:-}" ]]; then
        env LD_PRELOAD="$FADRIO_SANITIZER_PRELOAD" dotnet "$@"
    else
        dotnet "$@"
    fi
}

read_apps_until() {
    local pattern="$1"
    local output=""
    for _ in {1..5}; do
        output="$(run_dotnet run --project "$cli_project" --no-build -- apps)"
        if rg -F "$pattern" <<<"$output" >/dev/null; then
            printf '%s\n' "$output"
            return
        fi
        sleep 0.2
    done
    echo "Timed out waiting for application snapshot containing '$pattern':" >&2
    printf '%s\n' "$output" >&2
    for log_path in "$runtime_directory"/*.log; do
        echo "--- $log_path" >&2
        sed -n '1,160p' "$log_path" >&2
    done
    exit 1
}

assert_contains() {
    local output="$1"
    local pattern="$2"
    if ! rg -F "$pattern" <<<"$output" >/dev/null; then
        echo "Application snapshot did not contain '$pattern':" >&2
        printf '%s\n' "$output" >&2
        exit 1
    fi
}

assert_process_running() {
    local process_id="$1"
    local log_path="$2"
    if ! kill -0 "$process_id" 2>/dev/null; then
        echo "Harness process exited unexpectedly; log follows:" >&2
        sed -n '1,200p' "$log_path" >&2
        exit 1
    fi
}

start_pipewire "$runtime_directory/pipewire-initial.log"

bash -c 'exec "$1" --playback --target 0 --rate 48000 --channels 2 --format s16 --volume 0.8 \
    -P "{ application.name = \"Firefox\" application.id = \"org.mozilla.firefox\" application.icon-name = \"firefox\" application.process.id = \"$$\" }" -' \
    _ "$firefox_fixture" </dev/zero >"$runtime_directory/stream-one.log" 2>&1 &
first_stream_pid=$!
bash -c 'exec "$1" --playback --target 0 --rate 48000 --channels 2 --format s16 --volume 0.8 \
    -P "{ application.name = \"Firefox\" application.id = \"org.mozilla.firefox\" application.icon-name = \"firefox\" application.process.id = \"$$\" }" -' \
    _ "$firefox_fixture" </dev/zero >"$runtime_directory/stream-two.log" 2>&1 &
second_stream_pid=$!

sleep 0.25
assert_process_running "$first_stream_pid" "$runtime_directory/stream-one.log"
assert_process_running "$second_stream_pid" "$runtime_directory/stream-two.log"

cli_project="$repository_root/src/Fadrio.Cli/Fadrio.Cli.csproj"
initial="$(read_apps_until "Sessions: 2")"
assert_contains "$initial" "Application: Firefox"
assert_contains "$initial" "Canonical ID: xdg:firefox"
assert_contains "$initial" "Confidence: High"
assert_contains "$initial" "Icon: firefox"
assert_contains "$initial" "Volume: 100%"

run_dotnet run --project "$cli_project" --no-build -- \
    set xdg:firefox 35 >/dev/null
after_volume="$(read_apps_until "Volume: 35%")"
assert_contains "$after_volume" "Sessions: 2"

run_dotnet run --project "$cli_project" --no-build -- \
    mute xdg:firefox >/dev/null
after_mute="$(read_apps_until "Muted: true")"

kill "$second_stream_pid"
wait "$second_stream_pid" 2>/dev/null || true
second_stream_pid=""
after_removal="$(read_apps_until "Sessions: 1")"

bash -c 'exec "$1" --playback --target 0 --rate 48000 --channels 2 --format s16 --volume 0.8 \
    -P "{ application.name = \"Firefox\" application.id = \"org.mozilla.firefox\" application.icon-name = \"firefox\" application.process.id = \"$$\" }" -' \
    _ "$firefox_fixture" </dev/zero >"$runtime_directory/stream-recreated.log" 2>&1 &
second_stream_pid=$!
after_recreation="$(read_apps_until "Sessions: 2")"
assert_contains "$after_recreation" "Canonical ID: xdg:firefox"
assert_contains "$after_recreation" "Confidence: High"

kill "$second_stream_pid"
wait "$second_stream_pid" 2>/dev/null || true
second_stream_pid=""

run_dotnet run --project "$cli_project" --no-build -- apps --watch \
    >"$runtime_directory/watch.log" 2>&1 &
watcher_pid=$!
wait_for_pattern "$runtime_directory/watch.log" "Canonical ID: xdg:firefox"

churn_properties='{ application.name = "Fadrio Churn Fixture" application.id = "dev.fglabs.Fadrio.ChurnFixture" }'
for index in {1..12}; do
    pw-cat --playback --target 0 --rate 48000 --channels 2 --format s16 --volume 0.1 \
        -P "$churn_properties" - </dev/zero >"$runtime_directory/churn-$index.log" 2>&1 &
    churn_pid=$!
    sleep 0.08
    kill "$churn_pid" 2>/dev/null || true
    wait "$churn_pid" 2>/dev/null || true
    churn_pid=""
done
wait_for_pattern "$runtime_directory/watch.log" "Canonical ID: pipewire:dev.fglabs.fadrio.churnfixture"
if ! kill -0 "$watcher_pid" 2>/dev/null; then
    echo "The watching client exited during rapid node churn." >&2
    exit 1
fi

kill "$first_stream_pid"
wait "$first_stream_pid" 2>/dev/null || true
first_stream_pid=""
kill "$pipewire_pid"
wait "$pipewire_pid" 2>/dev/null || true
pipewire_pid=""
rm -f -- "$runtime_directory/pipewire-0"

start_pipewire "$runtime_directory/pipewire-restarted.log"

restart_properties='{ application.name = "Fadrio Restart Fixture" application.id = "dev.fglabs.Fadrio.RestartFixture" }'
pw-cat --playback --target 0 --rate 48000 --channels 2 --format s16 --volume 0.8 \
    -P "$restart_properties" - </dev/zero >"$runtime_directory/stream-restart.log" 2>&1 &
first_stream_pid=$!

wait_for_pattern "$runtime_directory/watch.log" "Canonical ID: pipewire:dev.fglabs.fadrio.restartfixture"
if ! kill -0 "$watcher_pid" 2>/dev/null; then
    echo "The watching client exited instead of reconnecting." >&2
    exit 1
fi

after_restart="$(read_apps_until "Canonical ID: pipewire:dev.fglabs.fadrio.restartfixture")"
assert_contains "$after_restart" "Sessions: 1"
if [[ "$(rg -c '^Application:' <<<"$after_restart")" -ne 1 ]]; then
    echo "The rebuilt registry contained duplicate applications:" >&2
    printf '%s\n' "$after_restart" >&2
    exit 1
fi

run_dotnet run --project "$cli_project" --no-build -- \
    set pipewire:dev.fglabs.fadrio.restartfixture 45 >/dev/null
after_restart_volume="$(read_apps_until "Volume: 45%")"
assert_contains "$after_restart_volume" "Sessions: 1"

run_dotnet run --project "$cli_project" --no-build -- \
    mute pipewire:dev.fglabs.fadrio.restartfixture >/dev/null
after_restart_mute="$(read_apps_until "Muted: true")"
assert_contains "$after_restart_mute" "Sessions: 1"

echo "Isolated PipeWire integration test passed."
