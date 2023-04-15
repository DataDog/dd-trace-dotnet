FROM mcr.microsoft.com/dotnet/runtime:7.0.5-bullseye-slim-amd64 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# Download the latest version of the tracer but don't install yet
# RUN TRACER_VERSION=$(curl -s \https://api.github.com/repos/DataDog/dd-trace-dotnet/releases/latest | grep tag_name | cut -d '"' -f 4 | cut -c2-) \
    # && curl -Lo /tmp/datadog-dotnet-apm.deb https://github.com/DataDog/dd-trace-dotnet/releases/download/v${TRACER_VERSION}/datadog-dotnet-apm_${TRACER_VERSION}_amd64.deb

ENV TRACER_VERSION=2.28.0
RUN curl -Lo /tmp/datadog-dotnet-apm.deb https://github.com/DataDog/dd-trace-dotnet/releases/download/v${TRACER_VERSION}/datadog-dotnet-apm_${TRACER_VERSION}_amd64.deb

WORKDIR /src
COPY ["Samples.TraceAnnotations/Samples.TraceAnnotations.csproj", "Samples.TraceAnnotations/"]
RUN dotnet restore "Samples.TraceAnnotations/Samples.TraceAnnotations.csproj"
COPY . .

WORKDIR "/src/Samples.TraceAnnotations"
RUN dotnet build "Samples.TraceAnnotations.csproj" --self-contained --runtime linux-x64 -c Debug -o /app/build

FROM build AS publish
RUN dotnet publish "Samples.TraceAnnotations.csproj" --self-contained --runtime linux-x64  -c Debug -o /app/publish



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
ENV DD_TRACE_DEBUG=true

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Samples.TraceAnnotations.dll"]