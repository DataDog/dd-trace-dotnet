#!/usr/bin/env bash
set -euox pipefail

DOTNETSDK_VERSION=9.0.100

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
ROOT_DIR="$(dirname -- "$SCRIPT_DIR" )"
BUILD_DIR="$ROOT_DIR/../tracer/build/_build"
IMAGE_NAME="tonyredondo504/dd-trace-dotnet_alpine-base-debug-$DOTNETSDK_VERSION"

export DOCKER_DEFAULT_PLATFORM=linux/arm64


# Try to pull; if it fails, build locally.
if ! docker pull "$IMAGE_NAME"; then
  echo "Image $IMAGE_NAME not found on registry. Building locally..."
  docker build \
    --build-arg DOTNETSDK_VERSION="$DOTNETSDK_VERSION" \
    --tag "$IMAGE_NAME" \
    --file "$BUILD_DIR/docker/alpine_debug.dockerfile" \
    "$BUILD_DIR"
fi

docker run -it --rm \
    --mount type=bind,source="$ROOT_DIR",target=/project \
    --env NugetPackageDirectory=/project/packages \
    --env artifacts=/project/tracer/bin/artifacts \
    --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    -p 5003:5003 \
    -v /var/log/datadog:/var/log/datadog/dotnet \
    $IMAGE_NAME \
    /bin/bash
