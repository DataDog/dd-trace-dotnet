#!/usr/bin/env bash
set -ex

python "$REPOSITORY_ROOT/steps/compare-results.py" "$ARTIFACTS_DIR/dsm-enabled.json" "$ARTIFACTS_DIR/tracer-no-dsm.json"