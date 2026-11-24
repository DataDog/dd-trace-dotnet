#!/bin/sh
set -eu

# Arguments: optional docker-run options, then IMAGE COMMAND [ARGUMENTS...].
# Callers opt into extra capabilities, credentials and test-only metadata explicitly.
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
    --env CI_COMMIT_SHA \
    --env CI_COMMIT_BRANCH \
    --env LINUX_DOTNET_SDK_VERSION \
    --env LINUX_NATIVE_DOTNET_SDK_VERSION \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    --env NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true \
    "$@"
