#!/bin/sh
set -eu

# Arguments: optional docker-run options, then IMAGE COMMAND [ARGUMENTS...].
# Callers opt into extra capabilities, credentials and CI host detection explicitly.
if [ "$#" -lt 2 ]; then
    echo 'Usage: run-linux-container.sh [docker-run options] IMAGE COMMAND [ARGUMENTS...]' >&2
    exit 2
fi

exec docker run --rm \
    --mount "type=bind,source=${CI_PROJECT_DIR:?CI_PROJECT_DIR must identify the checkout},target=/project" \
    --env artifacts=/project/artifacts/output \
    --env CI=true \
    --env CI_JOB_ID \
    --env CI_PROJECT_NAME \
    --env CI_JOB_NAME_SLUG \
    --env CI_PROJECT_URL \
    --env CI_PROJECT_DIR=/project \
    --env CI_PROJECT_PATH \
    --env CI_PIPELINE_ID \
    --env CI_PIPELINE_IID \
    --env CI_PIPELINE_URL \
    --env CI_JOB_URL \
    --env CI_JOB_NAME \
    --env CI_JOB_STAGE \
    --env CI_COMMIT_SHA \
    --env CI_COMMIT_BRANCH \
    --env CI_COMMIT_TAG \
    --env CI_COMMIT_REF_NAME \
    --env CI_COMMIT_MESSAGE \
    --env CI_COMMIT_AUTHOR \
    --env CI_COMMIT_TIMESTAMP \
    --env CI_RUNNER_ID \
    --env CI_RUNNER_TAGS \
    --env CI_MERGE_REQUEST_SOURCE_BRANCH_SHA \
    --env CI_MERGE_REQUEST_TARGET_BRANCH_SHA \
    --env CI_MERGE_REQUEST_DIFF_BASE_SHA \
    --env CI_MERGE_REQUEST_TARGET_BRANCH_NAME \
    --env CI_MERGE_REQUEST_IID \
    --env LINUX_DOTNET_SDK_VERSION \
    --env LINUX_NATIVE_DOTNET_SDK_VERSION \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    --env NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true \
    "$@"
