#!/bin/bash
set -eu
cd /binaries
mkdir -p /opt/datadog
tar xzf $(ls datadog-dotnet-apm.tar.gz) -C /opt/datadog

LD_LIBRARY_PATH=/opt/datadog dotnet fsi --langversion:preview /binaries/query-versions.fsx

echo "dd-trace version: $(cat /binaries/LIBRARY_VERSION)"
echo "libddwaf version: $(cat /binaries/LIBDDWAF_VERSION)"
echo "appsec event rules version: $(cat /binaries/APPSEC_EVENT_RULES_VERSION)"
