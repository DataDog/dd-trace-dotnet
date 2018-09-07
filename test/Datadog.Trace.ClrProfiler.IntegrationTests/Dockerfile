FROM microsoft/dotnet-framework:4.7.2-sdk
WORKDIR /app
COPY bin/x64/Release/ .
RUN dotnet restore
ENTRYPOINT ["dotnet", "vstest", "Datadog.Trace.ClrProfiler.IntegrationTests"]
