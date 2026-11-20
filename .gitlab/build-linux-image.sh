#!/bin/sh
set -eu

# Arguments: Dockerfile base name, SDK version, image tag, optional stage.
docker build \
    --build-arg "DOTNETSDK_VERSION=${2}" \
    --tag "$3" \
    --target "${4:-builder}" \
    --file "tracer/build/_build/docker/${1}.dockerfile" \
    tracer/build/_build
