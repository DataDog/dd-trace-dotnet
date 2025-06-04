#!/usr/bin/env bash
set -euo pipefail

DOTNETSDK_VERSION=9.0.100

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
ROOT_DIR="$(dirname -- "$SCRIPT_DIR" )/.."
BUILD_DIR="$ROOT_DIR/tracer/build/_build"
IMAGE_NAME="tonyredondo504/dd-trace-dotnet_alpine-base-debug-$DOTNETSDK_VERSION"

export DOCKER_DEFAULT_PLATFORM=linux/arm64

# Initialize force_build flag to false
force_build=false

# Check for the --force argument
if [[ "$#" -gt 0 && "$1" == "--force" ]]; then
  echo "Force build argument detected. Image will be rebuilt."
  force_build=true
fi

# Build logic
build_image() {
  echo "Building image $IMAGE_NAME locally..."
  docker build \
    --build-arg DOTNETSDK_VERSION="$DOTNETSDK_VERSION" \
    --tag "$IMAGE_NAME" \
    --file "$BUILD_DIR/docker/alpine_debug.dockerfile" \
    "$BUILD_DIR"
}

# Conditional build based on force_build flag or image availability
if [[ "$force_build" == true ]]; then
  build_image
else
  # Try to pull; if it fails, build locally.
  if ! docker pull "$IMAGE_NAME"; then
    echo "Image $IMAGE_NAME not found on registry or pull failed."
    build_image
  else
    echo "Image $IMAGE_NAME successfully pulled from registry."
  fi
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
