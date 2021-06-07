#!/bin/bash
set -euxo pipefail

cd "$( dirname "${BASH_SOURCE[0]}" )"/../../

buildConfiguration=${buildConfiguration:-Debug}
publishTargetFramework=${publishTargetFramework:-netcoreapp3.1}

mkdir -p /var/log/datadog/dotnet

#https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dumps#collecting-dumps-on-crash
export COMPlus_DbgEnableMiniDump=1
export COMPlus_DbgMiniDumpType=4

cleanup() {

    # Collect run data
    mkdir /project/build_data

    cp /var/log/datadog/dotnet/* /project/build_data/
    cp /tmp/coredump* /project/build_data/ 2>/dev/null || :
}

trap cleanup SIGINT SIGTERM EXIT

dotnet vstest test/Datadog.Trace.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/publish/Datadog.Trace.IntegrationTests.dll --logger:trx --ResultsDirectory:test/Datadog.Trace.IntegrationTests/results

dotnet vstest test/Datadog.Trace.OpenTracing.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/publish/Datadog.Trace.OpenTracing.IntegrationTests.dll --logger:trx --ResultsDirectory:test/Datadog.Trace.OpenTracing.IntegrationTests/results

wait-for-it servicestackredis:6379 -- \
wait-for-it stackexchangeredis:6379 -- \
wait-for-it elasticsearch6:9200 -- \
wait-for-it elasticsearch5:9200 -- \
wait-for-it sqlserver:1433 -- \
wait-for-it mongo:27017 -- \
wait-for-it postgres:5432 -- \
wait-for-it -t 60 kafka-zookeeper:2181 -- \
wait-for-it -t 60 kafka-broker:9092 -- \
dotnet vstest test/Datadog.Trace.ClrProfiler.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/publish/Datadog.Trace.ClrProfiler.IntegrationTests.dll --logger:trx --ResultsDirectory:test/Datadog.Trace.ClrProfiler.IntegrationTests/results --TestCaseFilter:Category!=LinuxUnsupported
