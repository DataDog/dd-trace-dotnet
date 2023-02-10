#!/bin/bash
set -eu
cd /binaries
mkdir -p /opt/datadog
mkdir /app 
tar xzf $(ls datadog-dotnet-apm.tar.gz) -C /opt/datadog

ls /opt/datadog

LD_LIBRARY_PATH=/opt/datadog dotnet fsi --langversion:preview /binaries/query-versions.fsx
echo "LS /app"
ls /app
echo "DONE"
#echo "dd-trace version: $(cat /app/SYSTEM_TESTS_LIBRARY_VERSION)"
echo "libddwaf version: $(cat /app/SYSTEM_TESTS_LIBDDWAF_VERSION)"
#echo "appsec event rules version: $(cat /app/SYSTEM_TESTS_APPSEC_EVENT_RULES_VERSION)"
echo "DONE DONE"