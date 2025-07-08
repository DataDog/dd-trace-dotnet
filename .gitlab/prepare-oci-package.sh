#!/bin/bash

set -eo pipefail

PACKAGE_VERSION=$(< ../artifacts/version.txt)

if [ -n "$CI_COMMIT_TAG" ] || [ -n "$DOTNET_PACKAGE_VERSION" ]; then
  VERSION=$PACKAGE_VERSION
else
  VERSION="$PACKAGE_VERSION$CI_VERSION_SUFFIX.win-ssi"
fi

echo "VERSION=$VERSION"

if [ -z "$ARCH" ]; then
  ARCH=amd64
fi

if [ "$OS" == "linux" ]; then
  if [ "$ARCH" == "amd64" ]; then
    SUFFIX=""
  elif [ "$ARCH" == "arm64" ]; then
    SUFFIX=".arm64"
  else
    echo "Unsupported architecture: $ARCH"
    exit 1
  fi

  SRC_TAR="../artifacts/datadog-dotnet-apm-$PACKAGE_VERSION${SUFFIX}.tar.gz"

  if [ ! -f $SRC_TAR ]; then
     echo "$SRC_TAR was not found!"
     exit 1
  fi

  mkdir -p sources

  # extract the tarball, making sure to preserve the owner and permissions
  tar --same-owner -pxvzf $SRC_TAR -C sources

  cp ../artifacts/requirements.json sources/requirements.json

elif [ "$OS" == "windows" ]; then

  if [ "$ARCH" != "amd64" ]; then
    echo "Unsupported architecture: win-$ARCH"
    exit 0
  fi

  # unzip the tracer home directory, and remove the xml files
  mkdir -p sources/library
  unzip ../artifacts/windows/windows-tracer-home.zip -d sources/library
  find sources/library -type f -name "*.xml" -delete

  # Unzip the fleet installer to the sub-directory
  mkdir -p sources/installer
  unzip ../artifacts/windows/fleet-installer.zip -d sources/installer/

  # Copy the telemetry forwarder in too
  cp ../artifacts/windows/telemetry_forwarder.exe sources/installer/

fi

echo -n $VERSION > sources/version
