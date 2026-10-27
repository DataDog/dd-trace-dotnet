#!/bin/sh

run_nuke() {
    image="$1"
    shift

    docker run --rm \
        --cap-add=SYS_PTRACE \
        --mount "type=bind,source=${CI_PROJECT_DIR},target=/project" \
        --env NugetPackageDirectory=/project/packages \
        --env artifacts=/project/artifacts/output \
        --env CI=true \
        --env CI_JOB_ID \
        --env CI_PROJECT_NAME \
        --env CI_JOB_NAME_SLUG \
        --env CI_COMMIT_SHA \
        --env CI_COMMIT_BRANCH \
        --env NUKE_TELEMETRY_OPTOUT=1 \
        --env NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true \
        "$image" \
        dotnet /build/bin/Debug/_build.dll "$@"
}
