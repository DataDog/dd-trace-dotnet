ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/WafRepo/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "WafRepo.csproj" -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

FROM $RUNTIME_IMAGE AS publish

RUN dnf install -y gdb

WORKDIR /app

# Copy the installer files from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /app/install

ARG INSTALL_CMD
RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog \
    && cd /app/install \
    && $INSTALL_CMD \
    && rm -rf /app/install

## SSI variables
ENV DD_INJECTION_ENABLED=tracer
ENV DD_INJECT_FORCE=1
ENV DD_TELEMETRY_FORWARDER_PATH=/bin/true

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

ENV ASPNETCORE_URLS=http://localhost:5000

# Capture dumps
ENV COMPlus_DbgEnableMiniDump=1
ENV COMPlus_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/dumps/coredump.%t.%p
ENV DOTNET_EnableCrashReport=1
ENV COMPlus_TieredCompilation=0
ENV DD_CLR_ENABLE_INLINING=0

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "WafRepo.dll"]
