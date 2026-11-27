#!/usr/bin/env bash

# Run the test suite in a Linux container.
#
# The suite exercises GNU coreutils and jq, which are not available on macOS.
# This script allows running it from macOS or any other host with Docker.
#
# Usage:
#   ./test.sh <any unittest arguments>
#
# Examples:
#   ./test.sh -v
#   ./test.sh --no-rebuild

set -e

SCRIPT_DIR=$(cd "$(dirname "$(readlink "$0" || echo "$0")")" && pwd)

IMAGE_NAME="benchmarking-platform-test"

# Build image (use --no-rebuild to skip if image exists)
if [ "$1" != "--no-rebuild" ] || ! docker image inspect "$IMAGE_NAME" &>/dev/null; then
    docker build -f "$SCRIPT_DIR/tests/test-linux.Dockerfile" -t "$IMAGE_NAME" "$SCRIPT_DIR/tests"
fi
if [ "$1" = "--no-rebuild" ]; then shift; fi

# Run the suite with the repo mounted read-only; the tests write only to
# temporary directories. The mount covers .gitlab/benchmarks/ so the tests
# can also reach ../shared (wait-for-pipeline.sh).
docker run --rm \
    -v "$SCRIPT_DIR/..:/repo:ro" \
    "$IMAGE_NAME" \
    python3 -m unittest discover -s macrobenchmarks/tests "$@"
