#!/usr/bin/env bash

# fetch-results.sh exports AWS_PROFILE=ephemeral-infra-ci for its own S3 reads;
# unset it here so this upload uses the CI runner's default credentials instead.
unset AWS_PROFILE

PROJECT="${UPSTREAM_PROJECT_NAME:-$CI_PROJECT_NAME}"
BRANCH="${UPSTREAM_BRANCH:-$CI_COMMIT_REF_NAME}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

# BP_EXTERNAL_S3_URL overrides the default destination prefix (must stay within
# relenv-benchmarking-data) so an external orchestrator can collect results
# under a prefix it controls.
S3_URL="${BP_EXTERNAL_S3_URL:-s3://relenv-benchmarking-data/${PROJECT}/${BRANCH}/${CI_JOB_ID}/}"
[[ "$S3_URL" == s3://relenv-benchmarking-data/* ]] || { echo "BP_EXTERNAL_S3_URL must stay within relenv-benchmarking-data" >&2; exit 1; }

shopt -s nullglob
converted_files=("$ARTIFACTS_DIR"/candidate*.converted.json)
shopt -u nullglob

for file in "${converted_files[@]}"; do
    aws s3 cp --acl bucket-owner-full-control "$file" "$S3_URL" || echo "WARNING: failed to upload $(basename "$file") to external S3" >&2
done
