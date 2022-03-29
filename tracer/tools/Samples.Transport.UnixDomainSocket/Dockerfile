#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS appbase
ARG DD_API_KEY
ENV DD_API_KEY=$DD_API_KEY

EXPOSE 80
EXPOSE 443
WORKDIR /app

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-buster-slim as builder
ARG DOTNETSDK_VERSION=6.0.100
ENV DOTNETSDK_VERSION=$DOTNETSDK_VERSION

# Based on https://github.com/dotnet/dotnet-docker/blob/34c81d5f9c8d56b36cc89da61702ccecbf00f249/src/sdk/6.0/bullseye-slim/amd64/Dockerfile
# and https://github.com/dotnet/dotnet-docker/blob/1eab4cad6e2d42308bd93d3f0cc1f7511ac75882/src/sdk/5.0/buster-slim/amd64/Dockerfile
ENV \
    # Unset ASPNETCORE_URLS from aspnet base image
    ASPNETCORE_URLS= \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=true \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip

RUN apt-get update \
    && apt-get -y upgrade \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --fix-missing \
        git \
        procps \
        wget \
        curl \
        cmake \
        make \
        llvm \
        clang \
        gcc \
        build-essential \
        rpm \
        ruby \
        ruby-dev \
        rubygems \
    && gem install --no-document fpm \
    && rm -rf /var/lib/apt/lists/*

# Install the .NET SDK
RUN curl -sSL https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh  \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && rm ./dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
# Trigger first run experience by running arbitrary cmd
    && dotnet help

ENV CXX=clang++
ENV CC=clang

ARG BUILD_DIR=tracer/build/_build
ENV BUILD_DIR=$BUILD_DIR

# Copy the projects in
COPY . /repo
WORKDIR repo

# Run the build tool
#RUN dotnet build $BUILD_DIR
#RUN dotnet tracer/build/_build/bin/Debug/_build.dll BuildTracerHome

## Build and publish the sample app
ENV SAMPLES_PROJ=tracer/samples/Samples.Transport.UnixDomainSocket/Samples.Transport.UnixDomainSocket.csproj
RUN dotnet restore $SAMPLES_PROJ
RUN dotnet build $SAMPLES_PROJ -c Debug -o /app/build
RUN dotnet publish $SAMPLES_PROJ -c Debug -o /app/publish

FROM appbase AS final
# Copy the app
COPY --from=builder /app/publish /app

# Set datadog variables
ENV DD_ENV=uds-container

# Sometimes the trace agent fails to start
# These variables make sure the tracer uses the sockets even if they aren't ready in the container at start
ENV DD_APM_RECEIVER_SOCKET=/var/run/datadog/apm.socket
ENV DD_DOGSTATSD_SOCKET=/var/run/datadog/dsd.socket

# Configure the transport
# TODO: None of these work... This is apparently due to a difference in how services are started... need to set those variables differently.
# DD_APM_RECEIVER_SOCKET as an environment variable is not respected by the agent when installing in a container (https://s3.amazonaws.com/dd-agent/scripts/install_script.sh)
# Using "service datadog-agent start" does not pick up the environment variable but calling the trace-agent binary directly does work (/opt/datadog-agent/embedded/bin/trace-agent). The DD_APM_RECEIVER_SOCKET is picked up and used per the logs.
# Using "service datadog-agent start" works when datadog.yaml has the pipe configured: setting `apm_config.receiver_socket: /var/run/datadog/apm.socket` in the yaml is picked up and used per the logs.
#ENV DD_TRACE_AGENT_URL=/var/run/datadog/apm.socket
# ENV DD_APM_RECEIVER_SOCKET=/var/run/datadog/apm.socket
# ENV DD_APM_CONFIG_RECEIVER_SOCKET=/var/run/datadog/apm.socket
# ENV DD_TRACE_AGENT_UNIX_DOMAIN_SOCKET=/var/run/datadog/apm.socket

# Install the automatic instrumentation tracer
#ENV CORECLR_ENABLE_PROFILING=1
#ENV CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
#ENV CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
#ENV DD_DOTNET_TRACER_HOME=/opt/datadog
#RUN mkdir -p /opt/datadog
#COPY --from=builder /repo/tracer/bin/tracer-home /opt/datadog
#RUN mkdir -p /var/log/datadog

# Install the agent
ENV DD_RUNTIME_METRICS_ENABLED=true
ENV DD_SITE=datadoghq.com
RUN mkdir /var/run/datadog; \
    chmod -R a+rwX /var/run/datadog
# RUN chown dd-agent -R /var/run/datadog
RUN DD_AGENT_MAJOR_VERSION=7 bash -c "$(curl -L https://s3.amazonaws.com/dd-agent/scripts/install_script.sh)"

# The environment variables in this dockerfile are only applied to PID 1, whereas the agent runs as a service... 
# TODO: Set service variables, for now, work around this by modifying the datadog.yaml
RUN > /etc/datadog-agent/datadog.yaml
RUN printf "api_key: $DD_API_KEY\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "site: datadoghq.com\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "env: uds-container\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "use_dogstatsd: true\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "dogstatsd_socket: /var/run/datadog/dsd.socket\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "apm_config:\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "  enabled: true\n" >> /etc/datadog-agent/datadog.yaml
RUN printf "  receiver_socket: /var/run/datadog/apm.socket\n" >> /etc/datadog-agent/datadog.yaml

# Make sure this thing starts because it will fail sometimes
RUN service datadog-agent start
RUN sleep 4
RUN service datadog-agent start
RUN sleep 5

ENTRYPOINT ["dotnet", "Samples.Transport.UnixDomainSocket.dll"]
