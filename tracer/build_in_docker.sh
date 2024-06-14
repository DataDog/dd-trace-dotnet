#!/usr/bin/env bash
set -euox pipefail

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
ROOT_DIR="$(dirname -- "$SCRIPT_DIR" )"
IMAGE_NAME="dd-trace-dotnet/debian-local-builder"
OUTPUT_DIR_REL="shared/bin/monitoring-home"
OUTPUT_DIR_ABS="$ROOT_DIR/$OUTPUT_DIR_REL"

docker build \
   --build-arg DOTNETSDK_VERSION=8.0.100 \
   --tag $IMAGE_NAME \
   --file "$BUILD_DIR/tracer/build/_build/docker/debian.dockerfile" \
   --target local_builder \
   "$ROOT_DIR"

docker run -it --rm \
    --mount "type=bind,source=$OUTPUT_DIR_ABS,target=/project/$OUTPUT_DIR_REL" \
    --env artifacts=/project/tracer/bin/artifacts \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    $IMAGE_NAME \
    dotnet /project/tracer/build/_build/bin/Debug/_build.dll "$@"
