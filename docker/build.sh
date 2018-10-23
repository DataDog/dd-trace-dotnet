#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

cd "$DIR/.."

for config in Debug Release ; do
    for proj in Datadog.Trace Datadog.Trace.OpenTracing ; do
        dotnet publish -f netstandard2.0 -c $config src/$proj/$proj.csproj
    done

    for proj in Datadog.Trace.ClrProfiler.Managed ; do
        dotnet publish -f netcoreapp2.0 -c $config src/$proj/$proj.csproj
    done

    for sample in Samples.AspNetCoreMvc2 Samples.ConsoleCore Samples.Elasticsearch Samples.RedisCore Samples.SqlServer ; do
        dotnet publish -f netcoreapp2.0 -c $config samples/$sample/$sample.csproj
    done

    for proj in Datadog.Trace.ClrProfiler.IntegrationTests ; do
        dotnet publish -f netcoreapp2.0 -c $config test/$proj/$proj.csproj
    done
done
