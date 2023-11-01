ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework $PUBLISH_FRAMEWORK -o /src/publish

# Creating a placeolder file we can copy in the publish stage to create the logs folder
RUN touch /src/artifacts/.placeholder.txt

FROM $RUNTIME_IMAGE AS publish

WORKDIR /app

# Add and extract the installer files to the expected location
# from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
ADD --from=builder /src/artifacts/datadog-dotnet-apm*.tar.gz /opt/datadog

# Copy the placeholder file to create the logs directory
COPY --from=builder /src/artifacts/.placeholder.txt /var/log/datadog/dotnet/.placeholder.txt

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

ENV ASPNETCORE_URLS=http://localhost:5000

# Copy the app across
COPY --from=builder /src/publish /app/.

# The final image, with "manual" configuration
FROM publish as final

# Set the required env vars
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
ENV DD_DOTNET_TRACER_HOME=/opt/datadog
ENV LD_PRELOAD=/opt/datadog/continuousprofiler/Datadog.Linux.ApiWrapper.x64.so
ENV DD_PROFILING_ENABLED=1
ENV DD_APPSEC_ENABLED=1
ENV DD_TRACE_DEBUG=1

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]

# The final image, with "dd-dotnet" configuration
# Note that we _can't_ use dd-dotnet-sh in this case
# Because there _is_ no shell in the chiseled containers
FROM publish as dd-dotnet-final
ARG RUNTIME_ID

ENTRYPOINT ["/opt/datadog/$RUNTIME_ID/dd-dotnet", "run", "--set-env", "DD_PROFILING_ENABLED=1","--set-env", "DD_APPSEC_ENABLED=1","--set-env", "DD_TRACE_DEBUG=1", "--", "dotnet", "/app/AspNetCoreSmokeTest.dll"]