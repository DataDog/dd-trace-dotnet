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

## Build and publish the sample app
ENV SAMPLES_PROJ=tracer/test/test-applications/regression/TransportsTester/TransportsTester.Docker.csproj
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
ENV DD_RUNTIME_METRICS_ENABLED=true

ENTRYPOINT ["dotnet", "TransportsTester.Docker.dll"]
