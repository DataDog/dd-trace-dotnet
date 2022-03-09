#!/usr/bin/env bash
set -euox pipefail

# in case we are being run from outside this directory
cd "$(dirname "$0")"

ROOT_DIR="$(dirname $(pwd))"
BUILD_DIR="$ROOT_DIR/tracer/build/_build"
IMAGE_NAME="dd-trace-dotnet/debian-base"

docker build \
   --build-arg DOTNETSDK_VERSION=6.0.100 \
   --tag $IMAGE_NAME \
   --file "$BUILD_DIR/docker/debian.dockerfile" \
   "$BUILD_DIR"

docker run -it --rm \
    --mount type=bind,source="$ROOT_DIR",target=/project \
    --env NugetPackageDirectory=/project/packages \
    --env tracerHome=/project/shared/bin/monitoring-home/tracer \
    --env artifacts=/project/tracer/bin/artifacts \
    -p 5003:5003 \
    -v /ddlogs:/var/log/datadog/dotnet \
    $IMAGE_NAME \
    dotnet /build/bin/Debug/_build.dll "$@"
