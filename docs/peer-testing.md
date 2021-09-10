# [Datadog .NET AppSec] Peer-testing

Peer-testing is a test script that allows engineers outside of the .NET group to test the installation and basic functionality of the app sec components.

## Testing Locally

This assumes the agent is already running, it requires agent 7.31.0-rc.6 or later.

Install .NET (yes, it does run on Mac and Linux): https://dotnet.microsoft.com/download

Create the app

```
dotnet new webapp -n DotnetPeerTest
```

Run the app:

```
cd DotnetPeerTest
dotnet run DotnetPeerTest.csproj
```

Check it works:

```
curl -k https://localhost:5001
```

Add the Datadog package:

```
dotnet add package Datadog.Monitoring.Distribution --prerelease
```

Set the env var.

Windows

```
SET DD_ENV=peer-test
SET DD_SERVICE=dotnet-peer-test
SET DD_VERSION=1.0.0
SET DD_APPSEC_ENABLED=true
SET CORECLR_ENABLE_PROFILING=1
SET CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET CORECLR_PROFILER_PATH=%CD%\bin\Debug\net5.0\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll
SET DD_INTEGRATIONS=%CD%\bin\Debug\net5.0\datadog\integrations.json
SET DD_DOTNET_TRACER_HOME=%CD%\bin\Debug\net5.0\datadog
```

Linux

```
export DD_ENV=peer-test
export DD_SERVICE=dotnet-peer-test
export DD_VERSION=1.0.0
export DD_APPSEC_ENABLED=true
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
export CORECLR_PROFILER_PATH=`pwd`/bin/Debug/net5.0/datadog/win-x64/Datadog.Trace.ClrProfiler.Native.dll
export DD_INTEGRATIONS=`pwd`/bin/Debug/net5.0/datadog/integrations.json
export DD_DOTNET_TRACER_HOME=`pwd`/bin/Debug/net5.0/datadog
```


Run the app again:

```
dotnet run DotnetPeerTest.csproj
```

Test it works:

```
curl -k -A Arachni/v1.0 https://localhost:5001
```

## Docker

The agent is run in a docker file, it requires a valid key to be present it the environment variable DD_API_KEY.

Create the app:

```
dotnet new webapp -n DotnetPeerTest
```

Create a docker file:

```
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

# Download the latest version of the tracer but don't install yet
RUN TRACER_VERSION=$(curl -s \https://api.github.com/repos/DataDog/dd-trace-dotnet/releases/latest | grep tag_name | cut -d '"' -f 4 | cut -c2-) \
    && curl -Lo /tmp/datadog-dotnet-apm.deb https://github.com/DataDog/dd-trace-dotnet/releases/download/v${TRACER_VERSION}/datadog-dotnet-apm_${TRACER_VERSION}_amd64.deb

WORKDIR /src
COPY ["DotnetPeerTest/DotnetPeerTest.csproj", "DotnetPeerTest/"]
RUN dotnet restore "DotnetPeerTest/DotnetPeerTest.csproj"
COPY . .
WORKDIR "/src/DotnetPeerTest"
RUN dotnet build "DotnetPeerTest.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DotnetPeerTest.csproj" -c Release -o /app/publish

FROM base AS final

# Copy the tracer from build target
COPY --from=build /tmp/datadog-dotnet-apm.deb /tmp/datadog-dotnet-apm.deb
# Install the tracer
RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog \
    && dpkg -i /tmp/datadog-dotnet-apm.deb \
    && rm /tmp/datadog-dotnet-apm.deb

# Enable the tracer
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/opt/datadog
ENV DD_INTEGRATIONS=/opt/datadog/integrations.json
ENV DD_APPSEC_ENABLED=true

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DotnetPeerTest.dll"]
```

Create docker-compose.yml:

```
version: "3.9"
services:
  web:
    build:
      context: ./
      dockerfile: ./Dockerfile
    ports:
      - "8000:80"
    depends_on:
      - datadog-agent
    environment:
      DD_ENV: "peer-test"
      DD_SERVICE: "dotnet-peer-test"
      DD_VERSION: "1.0.0"
      DD_AGENT_HOST: "datadog-agent"
      DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED: "true"
      DD_RUNTIME_METRICS_ENABLED: "true"
      DD_APPSEC_ENABLED: "true"
  datadog-agent:
    image: "datadog/agent:7.31.0-rc.6"
    environment:
      DD_APM_ENABLED: "true"
      DD_APM_NON_LOCAL_TRAFFIC: "true"
      DD_DOGSTATSD_NON_LOCAL_TRAFFIC: "true"
      DD_API_KEY: ${DD_API_KEY}
```

Test it works:

```
curl -k -A Arachni/v1.0 https://localhost:8000
```
