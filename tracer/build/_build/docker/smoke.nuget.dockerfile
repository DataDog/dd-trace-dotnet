ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

# do an initial restore
RUN dotnet restore "AspNetCoreSmokeTest.csproj"

# install the package
RUN dotnet add package "Datadog.Trace.Bundle" --source /src/artifacts

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

FROM $RUNTIME_IMAGE AS publish

WORKDIR /app

RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog

# Copy the app across
COPY --from=builder /src/publish /app/.

ARG RELATIVE_PROFILER_PATH
ARG RELATIVE_APIWRAPPER_PATH

# Set the required env vars
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/app/${RELATIVE_PROFILER_PATH}
ENV LD_PRELOAD=/app/${RELATIVE_APIWRAPPER_PATH}
ENV DD_DOTNET_TRACER_HOME=/app/datadog
ENV DD_PROFILING_ENABLED=1
ENV DD_APPSEC_ENABLED=1
ENV DD_TRACE_DEBUG=1

ENV ASPNETCORE_URLS=http://localhost:5000

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]
