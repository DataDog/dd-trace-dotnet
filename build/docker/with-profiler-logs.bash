#!/bin/bash
set -uxo pipefail

mkdir -p /var/log/datadog/dotnet

eval "$@"
exitVal=$?

cat /var/log/datadog/dotnet/dotnet-tracer-native* \
| awk '
  /info/ {print "\033[32m" $0 "\033[39m"}
  /warn/ {print "\033[31m" $0 "\033[39m"}
'
exit $exitVal