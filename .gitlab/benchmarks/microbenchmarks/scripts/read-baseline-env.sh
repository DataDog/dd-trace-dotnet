#!/usr/bin/env bash
# Reads baseline environment variables from baseline_env_vars.txt and exports them
# with a BASELINE_ prefix to avoid overwriting current CI variables.
#
# This file is sourced by other scripts, not executed directly.

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"
env_file="$ARTIFACTS_DIR/baseline_env_vars.txt"

if [ ! -f "$env_file" ]; then
    echo "Warning: Baseline env vars file not found at $env_file"
    return 0 2>/dev/null || exit 0
fi

while IFS='=' read -r key value; do
    if [ -n "$key" ]; then
        # Remove trailing carriage return (Windows line endings)
        value=${value%$'\r'}
        export "BASELINE_$key=$value"
    fi
done < "$env_file"

echo "Loaded baseline environment variables:"
printenv | grep BASELINE || echo "  No BASELINE_* variables found"
