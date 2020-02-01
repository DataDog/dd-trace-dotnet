#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

$DIR/with-profiler-logs.bash \
    dotnet vstest --verbosity minimal --logger trx --results-directory $DIR/../test/Datadog.Trace.IntegrationTests/results $DIR/../test/Datadog.Trace.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/Datadog.Trace.IntegrationTests.dll

$DIR/with-profiler-logs.bash \
    dotnet vstest --verbosity minimal --logger trx --results-directory $DIR/../test/Datadog.Trace.OpenTracing.IntegrationTests/results $DIR/../test/Datadog.Trace.OpenTracing.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/Datadog.Trace.OpenTracing.IntegrationTests.dll

$DIR/with-profiler-logs.bash \
    wait-for-it servicestackredis:6379 -- \
    wait-for-it stackexchangeredis:6379 -- \
    wait-for-it elasticsearch6:9200 -- \
    wait-for-it elasticsearch5:9200 -- \
    wait-for-it sqlserver:1433 -- \
    wait-for-it mongo:27017 -- \
    wait-for-it postgres:5432 -- \
    dotnet vstest --verbosity minimal --logger trx --results-directory $DIR/../test/Datadog.Trace.ClrProfiler.IntegrationTests/results $DIR/../test/Datadog.Trace.ClrProfiler.IntegrationTests/bin/$buildConfiguration/$publishTargetFramework/Datadog.Trace.ClrProfiler.IntegrationTests.dll
