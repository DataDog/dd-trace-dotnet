#!/usr/bin/env bash
# Uploads converted benchmark results to an S3 prefix chosen by an external
# orchestrator, for flaky-benchmark monitoring: it triggers this pipeline N times
# with a distinct BP_EXTERNAL_S3_URL per sample, then collects every sample from
# the shared run prefix.
#
# No-op unless BP_EXTERNAL_S3_URL is set, so normal PR and master runs are
# unaffected. Never fails the job: a sample that doesn't upload should surface as
# a missing sample, not as a red benchmark pipeline.
#
# Required environment variables (only when BP_EXTERNAL_S3_URL is set):
#   BP_EXTERNAL_S3_URL - Destination prefix, e.g. s3://bucket/repo/external/<run>/sample-00/
#   AWS_REGION - AWS region
#
# Optional:
#   ARTIFACTS_DIR - Directory containing results (default: ./artifacts)

set -e

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

# GitLab forwards the literal '$BP_EXTERNAL_S3_URL' (not an empty string) when a
# bridge declares `BP_EXTERNAL_S3_URL: $BP_EXTERNAL_S3_URL` and the orchestrator
# left it unset, so the literal has to be treated as unset too.
if [ -z "$BP_EXTERNAL_S3_URL" ] || [ "$BP_EXTERNAL_S3_URL" = '$BP_EXTERNAL_S3_URL' ]; then
    echo "BP_EXTERNAL_S3_URL is not set. Skipping external S3 upload."
    exit 0
fi

if [[ "$BP_EXTERNAL_S3_URL" != s3://* ]]; then
    echo "WARNING: BP_EXTERNAL_S3_URL is not an s3:// URL: '$BP_EXTERNAL_S3_URL'"
    echo "Skipping external S3 upload."
    exit 0
fi

if [ -z "$AWS_REGION" ]; then
    echo "WARNING: AWS_REGION is not set. Skipping external S3 upload."
    exit 0
fi

shopt -s nullglob
converted_files=("$ARTIFACTS_DIR"/candidate*.converted.json)
shopt -u nullglob

if [ ${#converted_files[@]} -eq 0 ]; then
    echo "WARNING: No converted results found in $ARTIFACTS_DIR"
    echo "Make sure to run analyze-results.sh first. Skipping external S3 upload."
    exit 0
fi

# Same credential setup fetch-results.sh uses: the benchmarks run on ephemeral
# infrastructure, whose credentials are not in the job environment by default.
bp-infra setup --region "$AWS_REGION" --os "windows"
export AWS_PROFILE=ephemeral-infra-ci

echo "=== Uploading ${#converted_files[@]} converted result(s) to external S3 ==="
echo "Destination: $BP_EXTERNAL_S3_URL"

# The URL already encodes the per-sample prefix and carries its trailing slash.
# Collect matches that exact layout, so upload flat into it rather than deriving
# any sub-path.
upload_failed=false
for file in "${converted_files[@]}"; do
    basename=$(basename "$file")
    echo "Uploading $basename..."
    if ! aws s3 cp "$file" "${BP_EXTERNAL_S3_URL%/}/$basename" \
        --region "$AWS_REGION" \
        --profile "$AWS_PROFILE"; then
        echo "WARNING: Failed to upload $basename"
        upload_failed=true
    fi
done

if [ "$upload_failed" = true ]; then
    echo ""
    echo "WARNING: One or more uploads to $BP_EXTERNAL_S3_URL failed."
    echo "The orchestrator collecting this run will see a missing or partial sample."
    echo "This does not affect benchmark correctness. The CI job will continue."
else
    echo "All uploads to external S3 complete."
fi

exit 0
