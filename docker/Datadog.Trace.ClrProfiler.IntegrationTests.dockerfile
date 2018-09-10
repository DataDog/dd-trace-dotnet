FROM microsoft/dotnet-framework:4.7.2-sdk-windowsservercore-1709

WORKDIR /app
COPY ./samples/ ./samples/
COPY ./src/Datadog.Trace.ClrProfiler.Native/bin/ ./src/Datadog.Trace.ClrProfiler.Native/bin/
COPY ./test/Datadog.Trace.ClrProfiler.IntegrationTests/bin/ ./test/Datadog.Trace.ClrProfiler.IntegrationTests/bin/
COPY ./docker/Datadog.Trace.ClrProfiler.IntegrationTests.ps1 ./
ENTRYPOINT ["Powershell.exe", "-File", "C:\\app\\Datadog.Trace.ClrProfiler.IntegrationTests.ps1"]
