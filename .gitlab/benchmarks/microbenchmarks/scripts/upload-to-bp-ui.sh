#!/usr/bin/env bash
# Uploads converted benchmark results to the Benchmarking Platform UI service.
#
# Required environment variables:
#   CI_PROJECT_NAME - GitLab/GitHub project name
#
# Optional:
#   ARTIFACTS_DIR - Directory containing converted results (default: ./artifacts)
#
# Upload failures are logged as warnings and do not fail the CI job. Missing
# converted files are treated as an error since that indicates a pipeline failure.

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

upload_failed=false

for CONVERTED_JSON in "${converted_files[@]}"; do
    echo "Uploading $(basename "$CONVERTED_JSON") to Benchmarking Platform..."

    set +e
    response=$(curl --retry 3 --retry-max-time 300 \
        -s -w "\n%{http_code}" \
        --form file=@"$CONVERTED_JSON" \
        "https://benchmarking-service.us1.prod.dog/benchmarks/upload/$CI_PROJECT_NAME?baseline_commit_sha=${BASELINE_CI_COMMIT_SHORT_SHA:-}&baseline_ci_pipeline_id=${BASELINE_CI_PIPELINE_ID:-}")
    curl_exit=$?
    set -e

    STATUS_CODE=$(echo "$response" | tail -n1)
    RESPONSE_BODY=$(echo "$response" | head -n-1)

    if [ $curl_exit -ne 0 ]; then
        echo "Warning: curl failed (exit $curl_exit) while uploading $(basename "$CONVERTED_JSON")"
        upload_failed=true
        continue
    fi

    if [ "$STATUS_CODE" -ne 200 ]; then
        echo "Warning: Upload of $(basename "$CONVERTED_JSON") failed with status $STATUS_CODE"
        if [ -n "$RESPONSE_BODY" ]; then
            echo "  Response: $RESPONSE_BODY"
        fi
        upload_failed=true
        continue
    fi

    echo "  Uploaded successfully."
done

if [ "$upload_failed" = true ]; then
    echo ""
    echo "Warning: One or more uploads failed. Benchmark results were not fully recorded in the Benchmarking Platform UI."
    echo "This does not affect benchmark correctness. The CI job will continue."
else
    echo "All uploads complete."
fi

exit 0
