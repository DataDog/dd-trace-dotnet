#!/usr/bin/env bash
# Converts BenchmarkDotNet JSON results to the format expected by benchmark_analyzer.
#
# BenchmarkDotNet produces results with LaunchCount runs aggregated internally,
# so no cross-run aggregation is needed here.
#
# Converts both candidate (current run) and baseline (from master _latest) results.
#
# Note: Converting baseline results on every PR run is repeated work. To overcome it,
# we must port benchmark_analyzer to Windows first, but this will take a while :)
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

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

if [ ! -d "$ARTIFACTS_DIR" ]; then
    echo "ERROR: Artifacts directory '$ARTIFACTS_DIR' does not exist"
    exit 1
fi

# Load baseline env vars with BASELINE_ prefix
source "$SCRIPT_DIR/read-baseline-env.sh"

# Metadata for converted results
CI_JOB_DATE=$(date +%s)
CPU_MODEL="${CPU_MODEL:-Intel(R) Xeon(R) Platinum 8259CL}"
KERNEL_VERSION=$(uname -a || echo "Unknown")
FRAMEWORK="Benchmarkdotnet"

convert_one() {
    local input_file="$1" output_file="$2" result_file="$3"
    local baseline_or_candidate="$4" branch="$5" commit_sha="$6"
    local commit_date="$7" job_id="$8" pipeline_id="$9" job_date="${10}"

    echo "  Converting: $(basename "$input_file") -> $(basename "$output_file")"

    local exit_code=0
    benchmark_analyzer convert \
        --extra-params="{\
            \"baseline_or_candidate\":\"$baseline_or_candidate\", \
            \"cpu_model\":\"$CPU_MODEL\", \
            \"kernel_version\":\"$KERNEL_VERSION\", \
            \"ci_job_date\":\"$job_date\", \
            \"ci_job_id\":\"$job_id\", \
            \"ci_pipeline_id\":\"$pipeline_id\", \
            \"git_commit_sha\":\"$commit_sha\", \
            \"git_commit_date\":\"$commit_date\", \
            \"git_branch\":\"$branch\"\
        }" \
        --framework="$FRAMEWORK" \
        --outpath="$output_file" \
        "$input_file" || exit_code=$?

    if [ $exit_code -ne 0 ]; then
        echo "  ERROR: Failed to convert $(basename "$input_file") (exit $exit_code)"
        echo "fail" > "$result_file"
        return
    fi

    echo "ok" > "$result_file"
}

convert_results() {
    local BASELINE_OR_CANDIDATE=$1
    local BRANCH=$2
    local COMMIT_SHA=$3
    local COMMIT_DATE=$4
    local JOB_ID=$5
    local PIPELINE_ID=$6
    local JOB_DATE=$7

    echo "Converting $BASELINE_OR_CANDIDATE results..."

    local convert_dir
    convert_dir=$(mktemp -d)

    local pids=()
    local files_found=false
    for INPUT_FILE in "$ARTIFACTS_DIR/$BASELINE_OR_CANDIDATE".*.json; do
        [ -e "$INPUT_FILE" ] || continue
        [[ "$INPUT_FILE" == *.converted.json ]] && continue

        files_found=true

        local OUTPUT_FILE="${INPUT_FILE%.json}.converted.json"
        local result_file="$convert_dir/$(basename "$INPUT_FILE").result"

        convert_one "$INPUT_FILE" "$OUTPUT_FILE" "$result_file" \
            "$BASELINE_OR_CANDIDATE" "$BRANCH" "$COMMIT_SHA" \
            "$COMMIT_DATE" "$JOB_ID" "$PIPELINE_ID" "$JOB_DATE" &
        pids+=($!)
    done

    if [ "$files_found" = false ]; then
        rm -rf "$convert_dir"
        if [ "$BASELINE_OR_CANDIDATE" = "candidate" ]; then
            echo "  ERROR: No candidate results found in $ARTIFACTS_DIR"
            exit 1
        else
            echo "  WARNING: No $BASELINE_OR_CANDIDATE results found"
        fi
        return
    fi

    for pid in "${pids[@]}"; do
        wait "$pid" || true
    done

    local any_failed=false
    for result_file in "$convert_dir"/*.result; do
        if [ "$(cat "$result_file" 2>/dev/null)" != "ok" ]; then
            any_failed=true
            break
        fi
    done
    rm -rf "$convert_dir"

    if [ "$any_failed" = true ]; then
        if [ "$BASELINE_OR_CANDIDATE" = "candidate" ]; then
            echo "  ERROR: One or more candidate conversions failed"
            exit 1
        else
            echo "  WARNING: One or more $BASELINE_OR_CANDIDATE conversions failed"
        fi
    fi
}

# Convert candidate results
CANDIDATE_COMMIT_DATE=$(git show -s --format=%ct "$CI_COMMIT_SHORT_SHA" 2>/dev/null || echo "$CI_JOB_DATE")
convert_results "candidate" "$CI_COMMIT_REF_NAME" \
    "$CI_COMMIT_SHORT_SHA" "$CANDIDATE_COMMIT_DATE" "$CI_JOB_ID" "$CI_PIPELINE_ID" "$CI_JOB_DATE"

# Convert baseline results (from master _latest)
if [ -n "$BASELINE_CI_COMMIT_SHORT_SHA" ]; then
    BASELINE_COMMIT_DATE=$(git show -s --format=%ct "$BASELINE_CI_COMMIT_SHORT_SHA" 2>/dev/null || echo "${BASELINE_CI_JOB_DATE:-$CI_JOB_DATE}")
    BASELINE_JOB_DATE="${BASELINE_CI_JOB_DATE:-$BASELINE_COMMIT_DATE}"

    convert_results "baseline" "master" \
        "$BASELINE_CI_COMMIT_SHORT_SHA" "$BASELINE_COMMIT_DATE" "$BASELINE_CI_JOB_ID" "$BASELINE_CI_PIPELINE_ID" "$BASELINE_JOB_DATE"
else
    echo ""
    echo "WARNING: No baseline metadata found (BASELINE_CI_COMMIT_SHORT_SHA not set)."
    echo "This is expected on the first run or when no master baseline exists yet."
fi

echo ""
echo "Analysis complete!"
