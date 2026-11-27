#!/usr/bin/env bash
set -e

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"
ARTIFACTS_BUCKET_NAME="${ARTIFACTS_BUCKET_NAME:-windows-benchmarking-results-us-east-1}"
mkdir -p "$ARTIFACTS_DIR"

bp-infra setup --region "$AWS_REGION" --os "windows"
export AWS_PROFILE=ephemeral-infra-ci

s3_prefix="$CI_PROJECT_NAME/$CI_COMMIT_REF_NAME/$CI_JOB_ID"
s3_path="s3://$ARTIFACTS_BUCKET_NAME/$s3_prefix/benchmark-artifacts.zip"
local_zip="$ARTIFACTS_DIR/benchmark-artifacts.zip"

echo "Downloading benchmark artifacts from S3..."
# The AWS standard policy retries transient failures, but not missing objects or
# access errors. Bound the entire copy as well as individual socket operations.
AWS_RETRY_MODE=standard AWS_MAX_ATTEMPTS=4 \
    timeout --kill-after=5s 300s aws s3 cp "$s3_path" "$local_zip" \
        --region "$AWS_REGION" --cli-connect-timeout 15 --cli-read-timeout 60 || {
    echo "WARNING: Could not download benchmark artifacts"
    exit 0
}

if [ -f "$local_zip" ]; then
    # unzip returns 1 for warnings (e.g., Windows backslash paths), so we tolerate it
    unzip -o "$local_zip" -d "$ARTIFACTS_DIR/" || [ $? -le 1 ]
    rm -f "$local_zip"
fi

if [ "$BP_INFRA_TEST" = "true" ]; then
    echo "BP_INFRA_TEST set to '$BP_INFRA_TEST'. Will mock benchmark results."
    exit 0
fi

# k6 results are saved as .json.gz to reduce size, decompress them
echo "Decompressing k6 results..."
for gz_file in "$ARTIFACTS_DIR"/*.json.gz; do
    [ -e "$gz_file" ] || continue
    gunzip "$gz_file"
done

echo "Converting raw k6 results to BP format..."

CPU_MODEL="Intel(R) Xeon(R) Platinum 8259CL"
CI_JOB_DATE=$(date +%s)
COMMIT_DATE=$(git show -s --format=%ct "$CI_COMMIT_SHA")

for raw_file in "$ARTIFACTS_DIR"/*.json; do
    [ -e "$raw_file" ] || continue

    filename=$(basename "$raw_file" .json)
    converted_file="$ARTIFACTS_DIR/candidate-$filename.converted.json"

    echo "Converting $filename..."

    benchmark_analyzer convert \
        --framework=k6 \
        --extra-params="{\"cpu_model\":\"$CPU_MODEL\", \"ci_job_date\":\"$CI_JOB_DATE\", \"ci_job_id\":\"$CI_JOB_ID\", \"ci_pipeline_id\":\"$CI_PIPELINE_ID\", \"git_commit_sha\":\"$CI_COMMIT_SHA\", \"git_commit_date\":\"$COMMIT_DATE\", \"git_branch\":\"$CI_COMMIT_REF_NAME\"}" \
        --metrics='["agg_http_req_duration_min", "agg_http_req_duration_p50", "agg_http_req_duration_p75", "agg_http_req_duration_p95", "agg_http_req_duration_p99", "agg_http_req_duration_max", "throughput", "dropped_iterations", "data_sent", "data_received"]' \
        --outpath="$converted_file" \
        "$raw_file"

    echo "    $raw_file -> $converted_file"
    rm -f "$raw_file"
done

echo "Results fetched and converted successfully!"
