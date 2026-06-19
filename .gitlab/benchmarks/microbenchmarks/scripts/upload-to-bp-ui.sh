#!/usr/bin/env bash

set -e

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

# Load baseline env vars with BASELINE_ prefix
source "$SCRIPT_DIR/read-baseline-env.sh"

shopt -s nullglob
converted_files=("$ARTIFACTS_DIR"/candidate*.converted.json)
shopt -u nullglob

if [ ${#converted_files[@]} -eq 0 ]; then
    echo "ERROR: No converted results found in $ARTIFACTS_DIR"
    echo "Make sure to run analyze-results.sh first."
    exit 1
fi

result_dir=$(mktemp -d)
trap 'rm -rf "$result_dir"' EXIT

upload_one() {
    local file="$1" result_file="$2"
    local basename
    basename=$(basename "$file")

    echo "Uploading $basename to Benchmarking Platform..."

    local response status_code response_body curl_exit=0
    response=$(curl --retry 3 --retry-all-errors --retry-max-time 300 \
        -s -w "\n%{http_code}" \
        --form file=@"$file" \
        "https://benchmarking-service.us1.prod.dog/benchmarks/upload/$CI_PROJECT_NAME?baseline_commit_sha=${BASELINE_CI_COMMIT_SHORT_SHA:-}&baseline_ci_pipeline_id=${BASELINE_CI_PIPELINE_ID:-}") || curl_exit=$?

    status_code=$(echo "$response" | tail -n1)
    response_body=$(echo "$response" | head -n-1)

    if [ $curl_exit -ne 0 ]; then
        echo "WARNING: curl failed (exit $curl_exit) while uploading $basename"
        echo "fail" > "$result_file"
        return
    fi

    if [ "$status_code" -ne 200 ]; then
        echo "WARNING: Upload of $basename failed with status $status_code"
        if [ -n "$response_body" ]; then
            echo "  Response: $response_body"
        fi
        echo "fail" > "$result_file"
        return
    fi

    echo "Uploaded $basename successfully."
    echo "ok" > "$result_file"
}

MAX_PARALLEL=4

pids=()
for CONVERTED_JSON in "${converted_files[@]}"; do
    result_file="$result_dir/$(basename "$CONVERTED_JSON").result"
    upload_one "$CONVERTED_JSON" "$result_file" &
    pids+=($!)

    if (( ${#pids[@]} >= MAX_PARALLEL )); then
        wait "${pids[0]}" || true
        pids=("${pids[@]:1}")
    fi
done

for pid in "${pids[@]}"; do
    wait "$pid" || true
done

upload_failed=false
for result_file in "$result_dir"/*.result; do
    if [ "$(cat "$result_file" 2>/dev/null)" != "ok" ]; then
        upload_failed=true
        break
    fi
done

if [ "$upload_failed" = true ]; then
    echo ""
    echo "WARNING: One or more uploads failed. Benchmark results were not fully recorded in the Benchmarking Platform UI."
    echo "This does not affect benchmark correctness. The CI job will continue."
else
    echo "All uploads complete."
fi

exit 0
