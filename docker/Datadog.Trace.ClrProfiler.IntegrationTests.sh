#!/bin/bash
set -euxo pipefail

cd "$( dirname "${BASH_SOURCE[0]}" )"/../

docker/with-profiler-logs.bash \
    dotnet vstest test/Datadog.Trace.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/publish/Datadog.Trace.IntegrationTests.dll --logger:trx --ResultsDirectory:test/Datadog.Trace.IntegrationTests/results

docker/with-profiler-logs.bash \
    dotnet vstest test/Datadog.Trace.OpenTracing.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/publish/Datadog.Trace.OpenTracing.IntegrationTests.dll --logger:trx --ResultsDirectory:test/Datadog.Trace.OpenTracing.IntegrationTests/results

docker/with-profiler-logs.bash \
    wait-for-it servicestackredis:6379 -- \
    wait-for-it stackexchangeredis:6379 -- \
    wait-for-it elasticsearch6:9200 -- \
    wait-for-it elasticsearch5:9200 -- \
    wait-for-it sqlserver:1433 -- \
    wait-for-it mongo:27017 -- \
    wait-for-it postgres:5432 -- \
    dotnet vstest test/Datadog.Trace.ClrProfiler.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/publish/Datadog.Trace.ClrProfiler.IntegrationTests.dll --logger:trx --ResultsDirectory:test/Datadog.Trace.ClrProfiler.IntegrationTests/results
