#!/bin/sh

# This script is used by the admission controller to install the library from the
# init container into the application container.

# Based on the user architecture,
archOutput="$(arch)"
case "$archOutput" in
    'x86_64')
        if [ -d linux_amd64 ]; then
            cp -R linux_amd64/* "$1"
        fi
        ;;
    'aarch64')
        if [ -d linux_arm64 ]; then
            cp -R linux_arm64/* "$1"
        fi
        ;;
    *)
        echo >&2 "error: unsupported architecture '$archOutput'"
        ;;
esac
