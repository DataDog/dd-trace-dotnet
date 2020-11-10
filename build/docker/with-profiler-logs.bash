#!/bin/bash
set -euxo pipefail

mkdir -p /var/log/datadog/dotnet
touch /var/log/datadog/dotnet/dotnet-tracer-native.log
tail -f /var/log/datadog/dotnet/dotnet-tracer-native.log | awk '
  /info/ {print "\033[32m" $0 "\033[39m"}
  /warn/ {print "\033[31m" $0 "\033[39m"}
' &

eval "$@"
