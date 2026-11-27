#!/usr/bin/env bash

set -ex

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

CLEANUP_ARG=$([[ "${CLEANUP}" == "false" ]] && echo "--no-cleanup" || echo "")

ALLOWED_ENV_VARS_REGEX="\
^DD_|^K6_|^BP_|^GITHUB_TOKEN|\
^ENDPOINT|^buildId|^DOTNET_BUILD_ARGS|\
^COR_PROFILER|^CORECLR_PROFILER|\
^COR_PROFILER_PATH|^CORECLR_PROFILER_PATH|\
^NATIVE_PROFILER_PATH|^TRACER_HOME_PATH|\
^COR_ENABLE_PROFILING|^CORECLR_ENABLE_PROFILING|\
^COMPlus_EnableDiagnostics"

echo "Passing environment variables to the ephemeral instance that match the following regex:"
echo "${ALLOWED_ENV_VARS_REGEX}"

# --bypass-stack-destroy cleans up instances faster
bp-infra launch \
  --bypass-stack-destroy \
  --region "${AWS_REGION}" \
  --provision "${SCRIPT_DIR}/../infrastructure/instance.yaml" \
  --env-regex "${ALLOWED_ENV_VARS_REGEX}" \
  ${CLEANUP_ARG:+$CLEANUP_ARG}
