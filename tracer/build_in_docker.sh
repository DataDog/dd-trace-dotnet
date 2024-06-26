#!/usr/bin/env bash
set -euox pipefail

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
ROOT_DIR="$(dirname -- "$SCRIPT_DIR" )"
BUILD_DIR="$ROOT_DIR/tracer/build/_build"
IMAGE_NAME="dd-trace-dotnet/alpine-base"

docker build \
   --build-arg DOTNETSDK_VERSION=8.0.100 \
   --tag $IMAGE_NAME \
   --file "$BUILD_DIR/docker/alpine.dockerfile" \
   "$BUILD_DIR"

docker run -it --rm \
    --mount type=bind,source="$ROOT_DIR",target=/project \
    --env NugetPackageDirectory=/project/packages \
    --env artifacts=/project/tracer/bin/artifacts \
    --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    -p 5003:5003 \
    -v /var/log/datadog:/var/log/datadog/dotnet \
    $IMAGE_NAME \
    dotnet /build/bin/Debug/_build.dll "$@"
