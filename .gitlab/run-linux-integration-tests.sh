#!/bin/sh

set -eu

base_tester_image="dd-trace-dotnet/${BASE_IMAGE}-integration-base:${CI_JOB_ID}"
tester_image="dd-trace-dotnet/${BASE_IMAGE}-tester:${DOTNET_SDK_VERSION}"
include_minor_package_versions=false
include_all_test_frameworks=true
test_suite="${TEST_SUITE:-integration}"
area="${AREA:-Tracer}"
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

# GitLab checks out the pipeline commit with a detached HEAD, while Azure's
# clone step checks out the target branch. Some CI Visibility integration tests
# exercise branch-based Git diffs, so reproduce Azure's branch checkout before
# mounting the repository in the test container.
if [ -n "${CI_COMMIT_REF_NAME:-}" ] && [ -z "$(git branch --show-current)" ]; then
  echo "Attaching the integration-test checkout to ${CI_COMMIT_REF_NAME}"
  git checkout -B "$CI_COMMIT_REF_NAME" "$CI_COMMIT_SHA"
fi

# Azure publishes the universal loader/wrapper from linux-musl-x64 and
# downloads it into the target runtime folder. Recreate that merge for the
# glibc job after GitLab downloads the independent producer artifacts. The
# Alpine job already uses linux-musl-x64 as its target folder.
mkdir -p "artifacts/monitoring-home/${ARTIFACT_SUFFIX}" artifacts/build_data/infra_logs
if [ "$ARTIFACT_SUFFIX" != "linux-musl-x64" ]; then
  cp -a artifacts/monitoring-home/linux-musl-x64/. "artifacts/monitoring-home/${ARTIFACT_SUFFIX}/"
fi

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

case "$test_suite" in
  integration|docker)
    include_docker=false
    filter=""
    if [ "$test_suite" = "docker" ]; then
      include_docker=true
      if [ -z "${DOCKER_GROUP:-}" ]; then
        echo "DOCKER_GROUP is required for Docker integration tests" >&2
        exit 1
      fi
      filter="DockerGroup=${DOCKER_GROUP}"
    fi

    echo "Building ${test_suite} integration tests for ${FRAMEWORK} (area=${area}, filter=${filter})"
    docker run --rm \
      --cap-add=SYS_PTRACE \
      --mount "type=bind,source=${CI_PROJECT_DIR},target=/project" \
      --env NugetPackageDirectory=/project/packages \
      --env artifacts=/project/artifacts/output \
      --env CI=true \
      --env CI_JOB_ID \
      --env DD_LOGGER_DD_API_KEY \
      --env "IncludeAllTestFrameworks=${include_all_test_frameworks}" \
      --env "Filter=${filter}" \
      --env NUKE_TELEMETRY_OPTOUT=1 \
      --env NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true \
      "$tester_image" \
      dotnet /build/bin/Debug/_build.dll \
      BuildIntegrationTests CompileTrimmingSamples \
      --framework "$FRAMEWORK" \
      --IncludeTestsRequiringDocker "$include_docker" \
      --TestAllPackageVersions true \
      --IncludeMinorPackageVersions "$include_minor_package_versions" \
      --NugetPackageDirectory /project/packages
    ;;
  debugger)
    optimize="${OPTIMIZE:-true}"
    echo "Building Debugger integration tests for ${FRAMEWORK} (optimize=${optimize})"
    docker run --rm \
      --cap-add=SYS_PTRACE \
      --mount "type=bind,source=${CI_PROJECT_DIR},target=/project" \
      --env NugetPackageDirectory=/project/packages \
      --env artifacts=/project/artifacts/output \
      --env CI=true \
      --env CI_JOB_ID \
      --env DD_LOGGER_DD_API_KEY \
      --env NUKE_TELEMETRY_OPTOUT=1 \
      --env NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true \
      "$tester_image" \
      dotnet /build/bin/Debug/_build.dll \
      BuildDebuggerIntegrationTests \
      --framework "$FRAMEWORK" \
      --targetplatform x64 \
      --debugtype portable \
      --optimize "$optimize" \
      --TestAllPackageVersions true \
      --IncludeMinorPackageVersions "$include_minor_package_versions" \
      --NugetPackageDirectory /project/packages
    ;;
  *)
    echo "Unknown Linux integration test suite '${test_suite}'" >&2
    exit 1
    ;;
esac

run_test_container()
{
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
    --env Area="$area" \
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
    dotnet /build/bin/Debug/_build.dll "$@"
}

test_exit_code=0
case "$test_suite" in
  integration)
    echo "Running non-Docker ${area} integration tests for ${FRAMEWORK}"
    run_test_container RunIntegrationTests || test_exit_code=$?
    ;;
  debugger)
    echo "Running Debugger integration tests for ${FRAMEWORK}"
    run_test_container RunDebuggerIntegrationTests \
      --framework "$FRAMEWORK" \
      --targetplatform x64 \
      --debugtype portable \
      --optimize "${OPTIMIZE:-true}" || test_exit_code=$?
    ;;
  docker)
    # The repository's dependency orchestration is also used by Azure with
    # docker-compose v1. Keep the same implementation here; the GitLab runner's
    # postgres alias issue is handled explicitly below.
    if ! command -v docker-compose >/dev/null 2>&1; then
      echo "Installing Docker Compose v1"
      apt-get update
      apt-get install --yes --no-install-recommends docker-compose
      rm -rf /var/lib/apt/lists/*
    fi

    if command -v docker-compose >/dev/null 2>&1; then
      compose()
      {
        docker-compose "$@"
      }
    else
      echo "Docker Compose is unavailable in the Docker runner image" >&2
      exit 1
    fi

    compose_project="dd-trace-${CI_JOB_ID}-g${DOCKER_GROUP}"
    cleanup_compose()
    {
      compose -p "$compose_project" logs || true
      compose -p "$compose_project" down || true
    }
    trap cleanup_compose EXIT HUP INT TERM

    export COMPOSE_PROFILES="group${DOCKER_GROUP}"
    export baseImage="$BASE_IMAGE"
    export framework="$FRAMEWORK"
    export Filter="DockerGroup=${DOCKER_GROUP}"
    export IncludeAllTestFrameworks="$include_all_test_frameworks"
    export IncludeMinorPackageVersions="$include_minor_package_versions"
    export TestAllPackageVersions=true
    export DD_LOGGER_ENABLED=true
    export DD_LOGGER_DD_SERVICE=dd-trace-dotnet
    export DD_LOGGER_DD_TRACE_LOG_DIRECTORY=/project/artifacts/build_data/infra_logs
    export DD_LOGGER_DD_TAGS="test.configuration.job:${CI_JOB_NAME}"

    postgres_host_argument=""
    if [ "$DOCKER_GROUP" = "1" ]; then
      # The GitLab Docker runner does not consistently publish the postgres
      # service alias on the Compose network. Start it explicitly and provide
      # a deterministic hosts entry to the waiter and integration-test containers.
      compose -p "$compose_project" up -d postgres
      postgres_container="$(compose -p "$compose_project" ps -q postgres)"
      postgres_ip="$(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$postgres_container")"
      if [ -z "$postgres_ip" ]; then
        echo "Could not determine the PostgreSQL container IP" >&2
        exit 1
      fi

      postgres_host_argument="--add-host=postgres:${postgres_ip}"
    fi

    echo "Starting Docker dependency group ${DOCKER_GROUP}"
    # Deliberately leave postgres_host_argument unquoted: it is either empty or
    # one complete Compose argument.
    # shellcheck disable=SC2086
    compose -p "$compose_project" run --rm $postgres_host_argument "StartDependencies.Group${DOCKER_GROUP}"

    echo "Running Docker dependency group ${DOCKER_GROUP} integration tests for ${FRAMEWORK}"
    # shellcheck disable=SC2086
    compose -p "$compose_project" run --rm \
      $postgres_host_argument \
      -e IncludeTestsRequiringDocker=true \
      -e IncludeAllTestFrameworks \
      -e DD_LOGGER_ENABLED \
      -e DD_LOGGER_DD_API_KEY \
      -e DD_LOGGER_DD_SERVICE \
      -e DD_LOGGER_DD_TRACE_LOG_DIRECTORY \
      -e DD_LOGGER_DD_TAGS \
      -e GITLAB_CI \
      -e CI_PROJECT_URL \
      -e CI_PIPELINE_ID \
      -e CI_JOB_ID \
      -e CI_REPOSITORY_URL \
      -e CI_COMMIT_SHA \
      -e CI_COMMIT_BRANCH \
      -e CI_COMMIT_TAG \
      -e CI_COMMIT_REF_NAME \
      -e CI_PROJECT_DIR=/project \
      -e CI_PROJECT_PATH \
      -e CI_PROJECT_NAME \
      -e CI_PIPELINE_IID \
      -e CI_PIPELINE_URL \
      -e CI_JOB_URL \
      -e CI_JOB_NAME \
      -e CI_JOB_NAME_SLUG \
      -e CI_JOB_STAGE \
      -e CI_COMMIT_MESSAGE \
      -e CI_COMMIT_AUTHOR \
      -e CI_COMMIT_TIMESTAMP \
      -e CI_RUNNER_ID \
      -e CI_RUNNER_TAGS \
      -e CI_MERGE_REQUEST_SOURCE_BRANCH_SHA \
      -e CI_MERGE_REQUEST_TARGET_BRANCH_SHA \
      -e CI_MERGE_REQUEST_DIFF_BASE_SHA \
      -e CI_MERGE_REQUEST_TARGET_BRANCH_NAME \
      -e CI_MERGE_REQUEST_IID \
      IntegrationTests || test_exit_code=$?

    cleanup_compose
    trap - EXIT HUP INT TERM
    ;;
esac

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
