#!/bin/bash

set -eo pipefail

PACKAGE_VERSION=$(< ../artifacts/version.txt)

# Note: on a branch build PACKAGE_VERSION already carries a -dev.<sha> suffix (see
# build/Version.props), so the version below ends up with two uniqueness tokens, e.g.
# 3.56.0-dev.gabc1234<CI_VERSION_SUFFIX>. That is still valid semver, a valid OCI tag and still
# unique, and CI_VERSION_SUFFIX is owned by the external one-pipeline template rather than by us,
# so it is deliberately left alone. Master keeps the plain version, which is the path that
# actually ships dev OCI packages. The one real downside is that the sha sorts before the pipeline
# id, so the doubled version loses the pipeline-ordered sortability CI_VERSION_SUFFIX provides.
if [ -n "$CI_COMMIT_TAG" ] || [ -n "$DOTNET_PACKAGE_VERSION" ]; then
  VERSION=$PACKAGE_VERSION
else
  VERSION=$PACKAGE_VERSION$CI_VERSION_SUFFIX
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

  # This compatibility hook is only included in the lib-injection OCI package.
  mkdir -p sources/net
  cp ../artifacts/OpenTelemetry.AutoInstrumentation.StartupHook.dll sources/net/

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
