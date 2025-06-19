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

WORKDIR /app

# Copy the installer files from tracer/test/test-applications/regression/WafRepo/artifacts
COPY --from=builder /src/artifacts /app/install

ARG INSTALL_CMD
RUN mkdir -p /opt/datadog \
    && mkdir -p /var/log/datadog \
    && cd /app/install \
    && $INSTALL_CMD \
    && rm -rf /app/install

ENV ASPNETCORE_URLS=http://localhost:5000

# Capture dumps
ENV COMPlus_DbgEnableMiniDump=1
ENV COMPlus_DbgMiniDumpType=4
ENV DOTNET_DbgMiniDumpName=/dumps/coredump.%t.%p
ENV DOTNET_EnableCrashReport=1

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "WafRepo.dll"]