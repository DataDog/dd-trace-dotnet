#!/bin/sh

set -eu

base_tester_image="dd-trace-dotnet/${BASE_IMAGE}-integration-base:${CI_JOB_ID}"
tester_image="dd-trace-dotnet/${BASE_IMAGE}-tester:${DOTNET_SDK_VERSION}"
include_minor_package_versions=false
include_all_test_frameworks=true
if [ "${perform_comprehensive_testing:-false}" = "true" ]; then
  include_minor_package_versions=true
fi

# GitLab's checkout borrows objects from a runner-local cache. That cache is
# outside the bind-mounted repository, so make the checkout self-contained
# before tests exercise Git metadata from inside the nested container.
alternates_file="$(git rev-parse --git-path objects/info/alternates)"
if [ -f "$alternates_file" ]; then
  echo "Materializing Git objects for the integration-test container"
  git repack -a -d
  rm -f "$alternates_file"
fi

# Azure publishes the universal loader/wrapper from linux-musl-x64 and
# downloads it into the target runtime folder. Recreate that merge after
# GitLab downloads the independent producer artifacts.
mkdir -p artifacts/monitoring-home/linux-x64 artifacts/build_data/infra_logs
cp -a artifacts/monitoring-home/linux-musl-x64/. artifacts/monitoring-home/linux-x64/

echo "Building ${BASE_IMAGE} integration-test image for ${FRAMEWORK}"
docker build \
  --build-arg "DOTNETSDK_VERSION=${DOTNET_SDK_VERSION}" \
  --tag "$base_tester_image" \
  --target tester \
  --file "tracer/build/_build/docker/${BASE_IMAGE}.dockerfile" \
  tracer/build/_build

echo "Adding Datadog CA certificates to the integration-test image"
docker build \
  --build-arg "TESTER_IMAGE=${base_tester_image}" \
  --tag "$tester_image" \
  --file .gitlab/linux-unit-tests.dockerfile \
  .gitlab

if [ -z "${DD_LOGGER_DD_API_KEY:-}" ]; then
  if [ -z "${DD_STS_OIDC_TOKEN:-}" ]; then
    echo "DD_STS_OIDC_TOKEN is unavailable" >&2
    exit 1
  fi

  DD_LOGGER_DD_API_KEY="$(
    docker run --rm \
      --env DD_STS_OIDC_TOKEN \
      "$tester_image" \
      sh -eu -c '
        response="$(curl --fail --silent --show-error \
          --header "Authorization: Bearer ${DD_STS_OIDC_TOKEN}" \
          "https://dd-sts.us1.ddbuild.io/sts/datadog/exchange?policy=apm-sdks-api-key")"
        printf "%s" "$response" | sed -n '\''s/.*"api_key"[[:space:]]*:[[:space:]]*"\([^" ]*\)".*/\1/p'\''
      '
  )"
  if [ -z "$DD_LOGGER_DD_API_KEY" ]; then
    echo "The dd-sts response did not contain an API key" >&2
    exit 1
  fi

  export DD_LOGGER_DD_API_KEY
  echo "CI Visibility API key configured using dd-sts"
fi

echo "Building non-Docker Tracer integration tests for ${FRAMEWORK}"
docker run --rm \
  --cap-add=SYS_PTRACE \
  --mount "type=bind,source=${CI_PROJECT_DIR},target=/project" \
  --env NugetPackageDirectory=/project/packages \
  --env artifacts=/project/artifacts/output \
  --env CI=true \
  --env CI_JOB_ID \
  --env DD_LOGGER_DD_API_KEY \
  --env "IncludeAllTestFrameworks=${include_all_test_frameworks}" \
  --env NUKE_TELEMETRY_OPTOUT=1 \
  --env NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true \
  "$tester_image" \
  dotnet /build/bin/Debug/_build.dll \
  BuildIntegrationTests CompileTrimmingSamples \
  --framework "$FRAMEWORK" \
  --IncludeTestsRequiringDocker false \
  --TestAllPackageVersions true \
  --IncludeMinorPackageVersions "$include_minor_package_versions" \
  --NugetPackageDirectory /project/packages

echo "Running non-Docker Tracer integration tests for ${FRAMEWORK}"
test_exit_code=0
docker run --rm \
  --cap-add=SYS_PTRACE \
  --hostname integrationtests \
  --mount "type=bind,source=${CI_PROJECT_DIR},target=/project" \
  --env NugetPackageDirectory=/project/packages \
  --env artifacts=/project/artifacts/output \
  --env baseImage="$BASE_IMAGE" \
  --env framework="$FRAMEWORK" \
  --env CodeCoverageEnabled=false \
  --env IncludeTestsRequiringDocker=false \
  --env IncludeAllTestFrameworks="$include_all_test_frameworks" \
  --env TestAllPackageVersions=true \
  --env IncludeMinorPackageVersions="$include_minor_package_versions" \
  --env Area=Tracer \
  --env enable_crash_dumps=true \
  --env Verify_DisableClipboard=true \
  --env DiffEngine_Disabled=true \
  --env CONTAINER_HOSTNAME=http://integrationtests \
  --env CI=true \
  --env DD_LOGGER_ENABLED=true \
  --env DD_LOGGER_DD_API_KEY \
  --env DD_LOGGER_DD_SERVICE=dd-trace-dotnet \
  --env DD_LOGGER_DD_TRACE_LOG_PATH=/project/artifacts/build_data/infra_logs/integration-ci-visibility.log \
  --env "DD_LOGGER_DD_TAGS=test.configuration.job:${CI_JOB_NAME}" \
  --env GITLAB_CI \
  --env CI_PROJECT_URL \
  --env CI_PIPELINE_ID \
  --env CI_JOB_ID \
  --env CI_REPOSITORY_URL \
  --env CI_COMMIT_SHA \
  --env CI_COMMIT_BRANCH \
  --env CI_COMMIT_TAG \
  --env CI_COMMIT_REF_NAME \
  --env CI_PROJECT_DIR=/project \
  --env CI_PROJECT_PATH \
  --env CI_PROJECT_NAME \
  --env CI_PIPELINE_IID \
  --env CI_PIPELINE_URL \
  --env CI_JOB_URL \
  --env CI_JOB_NAME \
  --env CI_JOB_NAME_SLUG \
  --env CI_JOB_STAGE \
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
  "$tester_image" \
  dotnet /build/bin/Debug/_build.dll RunIntegrationTests || test_exit_code=$?

log_check_exit_code=0
docker run --rm \
  --mount "type=bind,source=${CI_PROJECT_DIR},target=/project" \
  --env NugetPackageDirectory=/project/packages \
  --env artifacts=/project/artifacts/output \
  --env CI=true \
  "$tester_image" \
  dotnet /build/bin/Debug/_build.dll CheckBuildLogsForErrors || log_check_exit_code=$?

if [ "$test_exit_code" -ne 0 ]; then
  echo "Integration tests exited with code ${test_exit_code}" >&2
  exit "$test_exit_code"
fi

if [ "$log_check_exit_code" -ne 0 ]; then
  echo "Build-log validation exited with code ${log_check_exit_code}" >&2
  exit "$log_check_exit_code"
fi
