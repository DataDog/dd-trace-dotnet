#!/usr/bin/env bash
# Converts BenchmarkDotNet JSON results to the format expected by benchmark_analyzer.
#
# BenchmarkDotNet produces results with LaunchCount runs aggregated internally,
# so no cross-run aggregation is needed here.
#
# Required environment variables:
#   CI_COMMIT_REF_NAME - Git branch name
#   CI_COMMIT_SHORT_SHA - Git commit SHA (short)
#   CI_JOB_ID - GitLab job ID
#   CI_PIPELINE_ID - GitLab pipeline ID
#
# Optional:
#   ARTIFACTS_DIR - Directory containing results (default: ./artifacts)

set -e

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

if [ ! -d "$ARTIFACTS_DIR" ]; then
    echo "ERROR: Artifacts directory '$ARTIFACTS_DIR' does not exist"
    exit 1
fi

# Metadata for converted results
CI_JOB_DATE=$(date +%s)
CPU_MODEL="${CPU_MODEL:-Intel(R) Xeon(R) Platinum 8259CL}"
KERNEL_VERSION=$(uname -a || echo "Unknown")
FRAMEWORK="Benchmarkdotnet"

convert_results() {
    local BASELINE_OR_CANDIDATE=$1
    local BRANCH=$2
    local COMMIT_SHA=$3
    local JOB_ID=$4
    local PIPELINE_ID=$5

    echo "Converting $BASELINE_OR_CANDIDATE results..."

    local COMMIT_DATE
    COMMIT_DATE=$(git show -s --format=%ct "$COMMIT_SHA" 2>/dev/null || echo "$CI_JOB_DATE")

    local files_found=false
    for INPUT_FILE in "$ARTIFACTS_DIR/$BASELINE_OR_CANDIDATE".*.json; do
        [ -e "$INPUT_FILE" ] || continue

        # Skip already converted files
        [[ "$INPUT_FILE" == *.converted.json ]] && continue

        files_found=true

        OUTPUT_FILE="${INPUT_FILE%.json}.converted.json"

        echo "  Converting: $(basename "$INPUT_FILE") -> $(basename "$OUTPUT_FILE")"

        benchmark_analyzer convert \
            --extra-params="{\
                \"baseline_or_candidate\":\"$BASELINE_OR_CANDIDATE\", \
                \"cpu_model\":\"$CPU_MODEL\", \
                \"kernel_version\":\"$KERNEL_VERSION\", \
                \"ci_job_date\":\"$CI_JOB_DATE\", \
                \"ci_job_id\":\"$JOB_ID\", \
                \"ci_pipeline_id\":\"$PIPELINE_ID\", \
                \"git_commit_sha\":\"$COMMIT_SHA\", \
                \"git_commit_date\":\"$COMMIT_DATE\", \
                \"git_branch\":\"$BRANCH\"\
            }" \
            --framework="$FRAMEWORK" \
            --outpath="$OUTPUT_FILE" \
            "$INPUT_FILE"
    done

    if [ "$files_found" = false ]; then
        echo "  Warning: No $BASELINE_OR_CANDIDATE results found"
    fi
}

# Convert candidate results
convert_results "candidate" "$CI_COMMIT_REF_NAME" "$CI_COMMIT_SHORT_SHA" "$CI_JOB_ID" "$CI_PIPELINE_ID"

echo "Analysis complete!"
