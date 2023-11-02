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

ENV ASPNETCORE_URLS=http://localhost:5000

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

ENTRYPOINT ["/app/datadog/dd-dotnet.sh", "run", "--set-env", "DD_PROFILING_ENABLED=1","--set-env", "DD_APPSEC_ENABLED=1","--set-env", "DD_TRACE_DEBUG=1", "--",  "dotnet", "/app/AspNetCoreSmokeTest.dll"]