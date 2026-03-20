#!/usr/bin/env bash
# Uploads converted benchmark results to the Benchmarking Platform UI service.
#
# Required environment variables:
#   CI_PROJECT_NAME - GitLab/GitHub project name
#
# Optional:
#   ARTIFACTS_DIR - Directory containing converted results (default: ./artifacts)
#   BASELINE_CI_COMMIT_SHORT_SHA - Baseline commit SHA for comparison
#   BASELINE_CI_PIPELINE_ID - Baseline pipeline ID

set -e

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

shopt -s nullglob
converted_files=("$ARTIFACTS_DIR"/candidate*.converted.json)
shopt -u nullglob

if [ ${#converted_files[@]} -eq 0 ]; then
    echo "ERROR: No converted results found in $ARTIFACTS_DIR"
    echo "Make sure to run analyze-results.sh first."
    exit 1
fi

for CONVERTED_JSON in "${converted_files[@]}"; do
    echo "Uploading $(basename "$CONVERTED_JSON") to Benchmarking Platform..."

    STATUS_CODE=$(curl --retry 3 --retry-max-time 300 \
        -s -o /dev/null -w "%{http_code}" \
        --form file=@"$CONVERTED_JSON" \
        "https://benchmarking-service.us1.prod.dog/benchmarks/upload/$CI_PROJECT_NAME?baseline_commit_sha=${BASELINE_CI_COMMIT_SHORT_SHA:-}&baseline_ci_pipeline_id=${BASELINE_CI_PIPELINE_ID:-}")

    if [ "$STATUS_CODE" -ne 200 ]; then
        echo "ERROR: Upload failed with status code $STATUS_CODE"
        exit 1
    fi

    echo "  Uploaded successfully!"
done

echo "All uploads complete!"
