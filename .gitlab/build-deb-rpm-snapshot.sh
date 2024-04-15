#!/bin/bash

source common_build_functions.sh


if [ -z "$ARCH" ]; then
  ARCH=amd64
fi


export DOTNET_PACKAGE_VERSION="2.5.0.dev.1"
echo -n $DOTNET_PACKAGE_VERSION > auto_inject-dotnet.version

cp auto_inject-dotnet.version datadog-dotnet-apm.dir/opt/datadog/version

# Build packages
fpm_wrapper "datadog-apm-library-dotnet" "$DOTNET_PACKAGE_VERSION" \
  --input-type dir \
  --chdir=datadog-dotnet-apm.dir/opt/datadog \
  --prefix "$LIBRARIES_INSTALL_BASE/dotnet" \
  --after-install datadog-dotnet-apm.dir/opt/datadog/createLogPath.sh \
  --url "https://github.com/DataDog/dd-trace-dotnet" \
  --license "Apache License 2.0" \
  --description "Datadog APM client library for .NET" \
  .=.
