#!/usr/bin/env bash
# Posts benchmark comparison results as a PR comment on GitHub.
#
# Compares candidate results against baseline (if available) and posts
# a formatted markdown comment to the PR.
#
# Required environment variables:
#   CI_PROJECT_NAME - GitLab/GitHub project name
#   CI_COMMIT_REF_NAME - Git branch name (used to find PR)
#
# Optional:
#   ARTIFACTS_DIR - Directory containing converted results (default: ./artifacts)
#   UNCONFIDENCE_THRESHOLD - Threshold for flagging uncertain results (default: 5.0%)

set -e

if [ "$CI_COMMIT_REF_NAME" = "master" ]; then
    echo "Skipping PR comment on master branch."
    exit 0
fi

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"
UNCONFIDENCE_THRESHOLD="${UNCONFIDENCE_THRESHOLD:-5.0}"

red='\033[1;91m'
normal='\033[0m'

# Check for baseline files
shopt -s nullglob
baseline_files=("$ARTIFACTS_DIR"/baseline*.converted.json)
candidate_files=("$ARTIFACTS_DIR"/candidate*.converted.json)
shopt -u nullglob

if [ ${#candidate_files[@]} -eq 0 ]; then
    echo "ERROR: No candidate results found in $ARTIFACTS_DIR"
    exit 1
fi

if [ ${#baseline_files[@]} -eq 0 ]; then
    echo "========================================"
    echo -e "${red}No baseline results available${normal}"
    echo "========================================"
    echo ""
    echo "PR comment will not be created because there are no baseline results to compare against."
    echo "This is expected for the first run on a new branch or when baseline data is not yet available."
    echo ""
    echo "To get baseline comparison:"
    echo "  1. Ensure benchmarks have run on master branch"
    echo "  2. Re-run this pipeline"
    echo ""
    exit 0
fi

# Check for missing baselines (candidate exists but baseline doesn't)
missing_baselines=()
for fcandidate in "${candidate_files[@]}"; do
    middle_part=$(basename "$fcandidate" | sed 's/^candidate\.//' | sed 's/\.converted\.json$//')
    fbaseline="$ARTIFACTS_DIR/baseline.$middle_part.converted.json"

    if [[ ! -f "$fbaseline" ]]; then
        missing_baselines+=("$middle_part")
    fi
done

if [ ${#missing_baselines[@]} -gt 0 ]; then
    echo "========================================"
    echo -e "${red}Missing baseline results${normal}"
    echo "========================================"
    echo ""
    echo "The following benchmarks are missing baseline results:"
    for name in "${missing_baselines[@]}"; do
        echo "  - $name"
    done
    echo ""
    echo "PR comment will still be created for benchmarks with baselines."
    echo ""
fi

# Run comparison
export UNCONFIDENCE_THRESHOLD

echo "Comparing benchmark results..."
benchmark_analyzer compare pairwise \
    --baseline='{"baseline_or_candidate":"baseline"}' \
    --candidate='{"baseline_or_candidate":"candidate"}' \
    --format='md-nodejs' \
    --outpath="$ARTIFACTS_DIR/comparison.md" \
    "$ARTIFACTS_DIR"/baseline*.converted.json "$ARTIFACTS_DIR"/candidate*.converted.json

echo "Posting PR comment..."
set +e
cat "$ARTIFACTS_DIR/comparison.md" | pr-commenter \
    --for-repo="$CI_PROJECT_NAME" \
    --for-pr="$CI_COMMIT_REF_NAME" \
    --header='Benchmarks' \
    --on-duplicate=replace
pr_commenter_exit=$?
set -e

if [ $pr_commenter_exit -ne 0 ]; then
    echo "Warning: pr-commenter failed (exit $pr_commenter_exit), PR comment was not posted."
    exit 0
fi

echo "PR comment posted successfully!"
