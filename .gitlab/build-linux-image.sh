#!/bin/sh
set -eu

# Manual publication only. Arguments: Dockerfile base name, SDK version, image tag, optional stage.
docker build \
    --build-arg "DOTNETSDK_VERSION=${2}" \
    --tag "$3" \
    --target "${4:-build-environment}" \
    --file "tracer/build/_build/docker/${1}.dockerfile" \
    tracer/build/_build
