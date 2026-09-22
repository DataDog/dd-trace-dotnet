#!/usr/bin/env bash
# Fetches benchmark results from S3 after bp-infra completes.
#
# Downloads both candidate results (from current run) and baseline results
# (from _latest, i.e., the last master run) for comparison.
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

# Prepare baseline results for analysis by renaming to baseline.* format.
# Handles two input formats:
#   1. candidate.Trace.Foo.json (from new master runs with in-instance renaming)
#   2. Benchmarks.Trace.Foo-report-full-compressed.json (legacy raw BenchmarkDotNet output)
#
# TODO: Remove legacy format handling once _latest is populated by a post-merge master run.
prepare_baseline_results() {
    local BASELINE_DIR=$1

    if [ ! -d "$BASELINE_DIR" ]; then
        echo "WARNING: Baseline directory $BASELINE_DIR not found"
        return
    fi

    echo "Preparing baseline results..."

    # Handle new format: candidate.*.json -> baseline.*.json
    for file in "$BASELINE_DIR"/candidate.*.json; do
        [ -e "$file" ] || continue
        filename=$(basename "$file")
        newname="${filename/candidate./baseline.}"
        echo "  $filename -> $newname"
        mv "$file" "$ARTIFACTS_DIR/$newname"
    done

    # Handle legacy format: Benchmarks.Trace.Foo-report-full-compressed.json -> baseline.Trace.Foo.json
    for file in "$BASELINE_DIR"/Benchmarks.*-report-full-compressed.json; do
        [ -e "$file" ] || continue
        filename=$(basename "$file")
        # Remove 'Benchmarks.' prefix and '-report-full-compressed.json' suffix
        middle_part=$(echo "$filename" | sed 's/^Benchmarks\.//' | sed 's/-report-full-compressed\.json$//')
        newname="baseline.$middle_part.json"
        echo "  $filename -> $newname"
        mv "$file" "$ARTIFACTS_DIR/$newname"
    done
}

# Setup AWS credentials for ephemeral infrastructure
bp-infra setup --region "$AWS_REGION" --os "windows"
export AWS_PROFILE=ephemeral-infra-ci

# Prepare candidate results for analysis.
# Normally, bp-runner renames files to candidate.*.json format inside the instance.
# However, in BP_INFRA_TEST mode, _latest files are used as mock candidates and may be
# in legacy format (Benchmarks.Trace.Foo-report-full-compressed.json).
#
# TODO: Remove legacy format handling once _latest is populated by a post-merge master run.
prepare_candidate_results() {
    echo "Preparing candidate results..."

    # Handle legacy format: Benchmarks.Trace.Foo-report-full-compressed.json -> candidate.Trace.Foo.json
    for file in "$ARTIFACTS_DIR"/Benchmarks.*-report-full-compressed.json; do
        [ -e "$file" ] || continue
        filename=$(basename "$file")
        # Remove 'Benchmarks.' prefix and '-report-full-compressed.json' suffix
        middle_part=$(echo "$filename" | sed 's/^Benchmarks\.//' | sed 's/-report-full-compressed\.json$//')
        newname="candidate.$middle_part.json"
        echo "  $filename -> $newname"
        mv "$file" "$ARTIFACTS_DIR/$newname"
    done
}

# Download candidate results (already in candidate.*.json format from instance)
S3_PREFIX="$CI_PROJECT_NAME/$CI_COMMIT_REF_NAME/$CI_JOB_ID/reports"
echo "=== Downloading candidate results ==="
echo "Source: s3://$BP_INFRA_ARTIFACTS_BUCKET_NAME/$S3_PREFIX"
aws s3 cp "s3://$BP_INFRA_ARTIFACTS_BUCKET_NAME/$S3_PREFIX" "$ARTIFACTS_DIR/" \
    --region "$AWS_REGION" \
    --profile "$AWS_PROFILE" \
    --recursive || echo "WARNING: No candidate results found in S3"

# Handle legacy format for candidate files (BP_INFRA_TEST mode)
prepare_candidate_results

# Download baseline results from _latest (master)
BASELINE_PREFIX="$CI_PROJECT_NAME/_latest"
BASELINE_DIR="$ARTIFACTS_DIR/baseline_raw"
mkdir -p "$BASELINE_DIR"

echo ""
echo "=== Downloading baseline results from master ==="
echo "Source: s3://$BP_INFRA_ARTIFACTS_BUCKET_NAME/$BASELINE_PREFIX"
aws s3 cp "s3://$BP_INFRA_ARTIFACTS_BUCKET_NAME/$BASELINE_PREFIX" "$BASELINE_DIR/" \
    --region "$AWS_REGION" \
    --profile "$AWS_PROFILE" \
    --recursive || echo "WARNING: No baseline results found in S3 (first run?)"

# Rename baseline files
echo ""
prepare_baseline_results "$BASELINE_DIR"

# Copy baseline_env_vars.txt if present (env_vars.txt from _latest becomes baseline_env_vars.txt)
if [ -f "$BASELINE_DIR/env_vars.txt" ]; then
    cp "$BASELINE_DIR/env_vars.txt" "$ARTIFACTS_DIR/baseline_env_vars.txt"
    echo "Copied baseline_env_vars.txt"
fi

echo ""
echo "=== Downloaded files ==="
ls -la "$ARTIFACTS_DIR/"*.json 2>/dev/null | head -30 || echo "No JSON files found"

echo ""
echo "Fetch complete!"
