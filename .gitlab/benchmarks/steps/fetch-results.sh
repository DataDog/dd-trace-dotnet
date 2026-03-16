#!/usr/bin/env bash
# Fetches benchmark results from S3 after bp-runner completes.
#
# bp-runner uploads results to S3 during how_to_run_benchmarks. This script
# downloads those results for analysis.
#
# Required environment variables:
#   BP_INFRA_ARTIFACTS_BUCKET_NAME - S3 bucket name
#   AWS_REGION - AWS region
#   CI_PROJECT_NAME - GitLab project name
#   CI_COMMIT_REF_NAME - Git branch name
#   CI_JOB_ID - GitLab job ID
#
# Optional:
#   ARTIFACTS_DIR - Local directory for results (default: ./artifacts)

set -e

if [[ -z "$BP_INFRA_ARTIFACTS_BUCKET_NAME" ]]; then
    echo "ERROR: BP_INFRA_ARTIFACTS_BUCKET_NAME is not set"
    exit 1
fi

if [[ -z "$AWS_REGION" ]]; then
    echo "ERROR: AWS_REGION is not set"
    exit 1
fi

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"
mkdir -p "$ARTIFACTS_DIR"

# Setup AWS credentials for ephemeral infrastructure
bp-infra setup --region "$AWS_REGION" --os "windows"
export AWS_PROFILE=ephemeral-infra-ci

# Download candidate results
S3_PREFIX="$CI_PROJECT_NAME/$CI_COMMIT_REF_NAME/$CI_JOB_ID/reports"
echo "Downloading candidate results from s3://$BP_INFRA_ARTIFACTS_BUCKET_NAME/$S3_PREFIX"
aws s3 cp "s3://$BP_INFRA_ARTIFACTS_BUCKET_NAME/$S3_PREFIX" "$ARTIFACTS_DIR/" \
    --region "$AWS_REGION" \
    --profile "$AWS_PROFILE" \
    --recursive || echo "Warning: No candidate results found in S3"

# Rename files to candidate.* format if needed
# bp-runner already names files as candidate.Trace.SpanBenchmark.json
echo "Downloaded files:"
ls -la "$ARTIFACTS_DIR/" || true

echo "Fetch complete!"
