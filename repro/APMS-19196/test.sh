#!/bin/bash
set -e
cd "$(dirname "$0")"

if docker compose version >/dev/null 2>&1; then DC="docker compose"; else DC="docker-compose"; fi
MODE="${1:-er}"
BUILD_ARGS=()
[ "${BUILD_NO_CACHE:-}" = "1" ] && BUILD_ARGS=(--no-cache)

hammer_crash_loop() {
    local max_iter="${1:-400}" sleep_s="${2:-0.08}" paths="${3:-/throw-unauthorized /}"
    local i p
    for i in $(seq 1 "$max_iter"); do
        for p in $paths; do
            R=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 2 --max-time 5 "http://localhost:5000${p}" 2>/dev/null || echo "CRASHED")
            if [ "$R" = "CRASHED" ] || [ "$R" = "000" ]; then
                echo "*** CRASH/empty response — iter $i GET $p => $R ***"
                return 0
            fi
        done
        sleep "$sleep_s"
    done
    return 1
}

wait_for_app() {
    for i in $(seq 1 30); do
        curl -s http://localhost:5000/ >/dev/null 2>&1 && return 0
        sleep 1
    done
    return 1
}

REPRO_OK=0
$DC down --remove-orphans 2>/dev/null || true

if [ "$MODE" = "instrument-all" ]; then
    $DC -f docker-compose.yml -f docker-compose.instrument-all.yml build "${BUILD_ARGS[@]}"
    $DC -f docker-compose.yml -f docker-compose.instrument-all.yml up -d
    DC="$DC -f docker-compose.yml -f docker-compose.instrument-all.yml"
else
    $DC build "${BUILD_ARGS[@]}"
    $DC up -d
fi

wait_for_app || { $DC logs app | tail -80; exit 1; }
echo "App up."

if [ "$MODE" = "instrument-all" ]; then
    hammer_crash_loop 500 0.08 && REPRO_OK=1 || true
else
    curl -s -o /dev/null -w "throw: %{http_code}\n" http://localhost:5000/throw
    hammer_crash_loop 200 0.05 "/throw-unauthorized" && REPRO_OK=1 || true
    [ "$REPRO_OK" -eq 0 ] && hammer_crash_loop 350 0.08 "/throw-unauthorized /" && REPRO_OK=1 || true
    [ "$REPRO_OK" -eq 0 ] && $DC logs app 2>&1 | grep -qiE "InvalidProgram|invalid program" && REPRO_OK=1 || true

    if [ "$REPRO_OK" -eq 0 ] && [ "${SKIP_INSTRUMENT_ALL_FALLBACK:-}" != "1" ]; then
        $DC down --remove-orphans 2>/dev/null || true
        $DC -f docker-compose.yml -f docker-compose.instrument-all.yml build "${BUILD_ARGS[@]}"
        $DC -f docker-compose.yml -f docker-compose.instrument-all.yml up -d
        DC="$DC -f docker-compose.yml -f docker-compose.instrument-all.yml"
        wait_for_app || exit 1
        hammer_crash_loop 500 0.08 && REPRO_OK=1 || true
        [ "$REPRO_OK" -eq 0 ] && $DC logs app 2>&1 | grep -qiE "InvalidProgram|invalid program" && REPRO_OK=1 || true
    fi
fi

echo "--- app logs (tail) ---"
$DC logs app 2>&1 | tail -60

if [ "${VERBOSE:-}" = "1" ]; then
    $DC exec -T app sh -c 'tail -80 /var/log/datadog/dotnet/dotnet-tracer-native-*.log 2>/dev/null' || true
fi

echo "Cleanup: $DC down"

if [ "$REPRO_OK" -eq 1 ]; then
    echo "RESULT: possible CLR failure (curl/grep)"
    exit 0
fi

if [ "${RUN_EH_STANDALONE:-}" = "1" ] && [ -f eh_sort_repro.cpp ] && command -v c++ >/dev/null 2>&1; then
    _b="$(mktemp)"
    if c++ -std=c++17 -o "$_b" eh_sort_repro.cpp && "$_b"; then
        rm -f "$_b"
        echo "RESULT: no Docker crash; eh_sort_repro OK (ordering defect only)"
        exit 0
    fi
    rm -f "$_b"
fi

echo "RESULT: no crash detected (try native Linux x86_64; see README)"
exit 1
