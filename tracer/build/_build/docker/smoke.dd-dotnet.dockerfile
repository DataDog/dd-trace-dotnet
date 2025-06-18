ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

FROM $RUNTIME_IMAGE AS publish

###############################################################################
# Fedora 35 (only) – glibc hot-fix to 2.37+  (arm64)
###############################################################################
RUN set -eux; \
    . /etc/os-release; \
    if [ "$ID" = "fedora" ] && [ "$VERSION_ID" = "35" ]; then \
        echo "[glibc-fix] Fedora 35 detected – upgrading glibc from 2.34 to 2.37+"; \
        dnf -y --releasever=38 --setopt=install_weak_deps=False upgrade \
            glibc glibc-common glibc-minimal-langpack \
            libgcc libstdc++; \
        dnf clean all; \
        /lib/ld-linux-aarch64.so.1 --version | head -n1; \
    else \
        echo "[glibc-fix] base is $ID $VERSION_ID – no glibc bump needed"; \
    fi
###############################################################################

WORKDIR /app

# Copy the installer files from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /app/install

ARG INSTALL_CMD
RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog \
    && cd /app/install \
    && $INSTALL_CMD \
    && rm -rf /app/install

# Set the required env vars
ENV ASPNETCORE_URLS=http://localhost:5000

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Capture dumps
ENV COMPlus_DbgEnableMiniDump=1
ENV COMPlus_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/dumps/coredump.%t.%p
ENV DOTNET_EnableCrashReport=1
ENV DD_REMOTE_CONFIGURATION_ENABLED=0

## SSI variables
ENV DD_INJECTION_ENABLED=tracer
ENV DD_INJECT_FORCE=1
ENV DD_TELEMETRY_FORWARDER_PATH=/bin/true

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["/opt/datadog/dd-dotnet.sh", "run", "--set-env", "DD_PROFILING_ENABLED=1","--set-env", "DD_APPSEC_ENABLED=1","--set-env", "DD_TRACE_DEBUG=1", "--", "dotnet", "/app/AspNetCoreSmokeTest.dll"]
