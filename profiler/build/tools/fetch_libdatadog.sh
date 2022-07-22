#!/bin/bash

# Unless explicitly stated otherwise all files in this repository are licensed under the Apache License Version 2.0.
# This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present Datadog, Inc.

# http://redsymbol.net/articles/unofficial-bash-strict-mode/
#set -euo pipefail
IFS=$'\n\t'


usage() {
    echo "Usage :"
    echo "$0 <github_release_version> <ARCH> <SHA256> <path>"
    echo ""
    echo "Example"
    echo "  $0 v0.2.0 x86_64 cba0f24074d44781d7252b912faff50d330957e84a8f40a172a8138e81001f27 ./vendor"
}

if [ $# != 4 ] || [ $1 == "-h" ]; then
    usage
    exit 1
fi

### Set directory names
CURRENTDIR=$PWD
SCRIPTPATH=$(readlink -f "$0")
SCRIPTDIR=$(dirname $SCRIPTPATH)
cd $SCRIPTDIR/../
TOP_LVL_DIR=$PWD
cd $CURRENTDIR

VER_LIBDATADOG=$1
ARCH_LIBDATADOG=$2
if [ -z "${IsAlpine}" ]; then
    TAR_LIBDATADOG=libdatadog-${ARCH_LIBDATADOG}-unknown-linux-gnu.tar.gz
else
    TAR_LIBDATADOG=libdatadog-${ARCH_LIBDATADOG}-alpine-linux-musl.tar.gz
fi

URL_LIBDATADOG=https://github.com/DataDog/libdatadog/releases/download/${VER_LIBDATADOG}/${TAR_LIBDATADOG}

SHA256_LIBDATADOG=$3
mkdir -p $4
cd $4
DOWNLOAD_PATH=$PWD
TARGET_EXTRACT=${DOWNLOAD_PATH}/libdatadog

if [ -e ${TARGET_EXTRACT} ]; then
    echo "Error, clean the directory : ${TARGET_EXTRACT}"
    exit 1
fi

mkdir -p ${TARGET_EXTRACT}

if [ ! -e  ${TAR_LIBDATADOG} ]; then
    # Http works locally
    echo "Download using curl... ${URL_LIBDATADOG}"
    curl -L ${URL_LIBDATADOG} -o ${TAR_LIBDATADOG}
fi

SHA_TAR=$(sha256sum ${DOWNLOAD_PATH}/${TAR_LIBDATADOG} | cut -d' ' -f1)

if [ $SHA_TAR != ${SHA256_LIBDATADOG} ];then
    echo "Error validating libdatadog"
    echo "Got following SHA: ${SHA_TAR} (instead of ${SHA256_LIBDATADOG})"
    echo "Please clear ${DOWNLOAD_PATH}/${TAR_LIBDATADOG} before restarting"
    exit 1
fi

tmp_dir=$(mktemp -d -t deliverables-XXXXXXXXXX)
echo "Extract to $tmp_dir"
cd $tmp_dir
tar xvfz ${DOWNLOAD_PATH}/${TAR_LIBDATADOG}
mv * ${TARGET_EXTRACT}
rmdir $tmp_dir
cd -
exit 0
