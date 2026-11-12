#!/bin/sh
set -eu

# global.json keeps the SDK version on its own line. Use tools already present
# in the Docker CLI image so resolving it needs no package install or SDK.
awk -F '"' '
    /^[[:space:]]*"version"[[:space:]]*:/ { version = $4; count++ }
    END {
        if (count != 1 || version !~ /^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$/) {
            print "Expected one SDK version in global.json" > "/dev/stderr"
            exit 1
        }
        print version
    }
' "${1:-global.json}"
