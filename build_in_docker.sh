#!/usr/bin/env bash
set -euox pipefail

# in case we are being run from outside this directory
cd "$(dirname "$0")"

ROOT_DIR="$(pwd)"
BUILD_DIR="$ROOT_DIR/build/_build"
IMAGE_NAME="dd-trace-dotnet/debian-base"

docker build \
   --build-arg DOTNETSDK_VERSION=5.0.103 \
   --tag $IMAGE_NAME \
   --file "$BUILD_DIR/docker/debian.dockerfile" \
   "$BUILD_DIR"

docker run -it --rm \
    --mount type=bind,source="$ROOT_DIR",target=/project \
    --env NugetPackageDirectory=/project/packages \
    --env tracerHome=/project/src/bin/windows-tracer-home \
    --env artifacts=/project/src/bin/artifacts \
    $IMAGE_NAME \
    dotnet /build/bin/Debug/_build.dll "$@"