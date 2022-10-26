ARG lambdaBaseImage
FROM $lambdaBaseImage

# Create log path
RUN mkdir -p /var/log/datadog/dotnet && \
    chmod a+rwx /var/log/datadog/dotnet

# Add Tracer
COPY ./bin/artifacts/monitoring-home /opt/datadog

ARG framework

# Add Tests
COPY ./bin/Release/$framework/*.dll /var/task/
COPY ./bin/Release/$framework/*.deps.json /var/task/
COPY ./bin/Release/$framework/*.runtimeconfig.json /var/task/

ENV DD_LOG_LEVEL="DEBUG"
ENV DD_TRACE_ENABLED=true
ENV DD_DOTNET_TRACER_HOME="/opt/datadog"
# The serverless artifacts used in this test don't include the hard links at the root folder
# so link to the arch-specific version instead
ENV _DD_EXTENSION_PATH="/opt/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"

ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
ENV CORECLR_PROFILER_PATH="/opt/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"

ENV AWS_LAMBDA_FUNCTION_NAME="my-test-function"