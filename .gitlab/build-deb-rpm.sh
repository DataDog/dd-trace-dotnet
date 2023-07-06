#!/bin/bash

source common_build_functions.sh

if [ -n "$CI_COMMIT_TAG" ] && [ -z "$DOTNET_PACKAGE_VERSION" ]; then
  DOTNET_PACKAGE_VERSION=${CI_COMMIT_TAG##v}
fi

curl --location --fail \
  --output datadog-dotnet-apm.old \
  "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$DOTNET_PACKAGE_VERSION/datadog-dotnet-apm_${DOTNET_PACKAGE_VERSION}_amd64.deb"

fpm --input-type deb \
  --output-type dir \
  --name datadog-dotnet-apm \
  datadog-dotnet-apm.old

echo -n $DOTNET_PACKAGE_VERSION > auto_inject-dotnet.version

cp auto_inject-dotnet.version datadog-dotnet-apm.dir/opt/datadog/version

# Build packages
fpm_wrapper "datadog-apm-library-dotnet" "$DOTNET_PACKAGE_VERSION" \
  --input-type dir \
  --chdir=datadog-dotnet-apm.dir/opt/datadog \
  --prefix "$LIBRARIES_INSTALL_BASE/dotnet" \
  --after-install datadog-dotnet-apm.dir/opt/datadog/createLogPath.sh \
  .=.