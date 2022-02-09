#!/bin/bash

# Unless explicitly stated otherwise all files in this repository are licensed under the Apache License Version 2.0.
# This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present Datadog, Inc.

# http://redsymbol.net/articles/unofficial-bash-strict-mode/
set -euo pipefail
IFS=$'\n\t'


usage() {
    echo "Usage :"
    echo "$0 <file_containing_version> <expected_version> <file_to_generate>"
    echo ""
    echo "Returns an error code if the version from the file does not match the expected version"
    echo ""
    echo "Example"
    echo "  $0 ../vendor/libddprof/version.txt d313bdd6 ./libddprof_version_check.txt"
}


# last line holds the version of ddprof
VERSION_FROM_FILE=$(cat $1 | tail -n 1)
VERSION_INPUT=$2
OUTPUT_CHECK=$3

if [ ${VERSION_FROM_FILE} != ${VERSION_INPUT} ]; then
    echo "--- Libddprof version does not match : ${VERSION_FROM_FILE} vs ${VERSION_INPUT}"
    exit 1
fi

echo "--- Libddprof version match : ${VERSION_FROM_FILE}"
echo "${VERSION_INPUT}" > ${OUTPUT_CHECK}
exit 0
