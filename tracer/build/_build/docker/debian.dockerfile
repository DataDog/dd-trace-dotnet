# We used a fixed, older version of debian for linking reasons
FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-buster-slim as base
ARG DOTNETSDK_VERSION

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
    NUGET_XMLDOC_MODE=skip \
    # Disable LTTng tracing with QUIC
    QUIC_LTTng=0

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
        uuid-dev \
        autoconf \
        libtool \
        liblzma-dev \
        gdb \
        cppcheck \
		zlib1g-dev \
    && gem install --version 1.6.0 --user-install git \
    && gem install --version 2.7.6 dotenv \
    && gem install --version 1.14.2 --minimal-deps --no-document fpm \
    && rm -rf /var/lib/apt/lists/*

# Install the .NET SDK
RUN curl -sSL https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh  \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && rm ./dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
# Trigger first run experience by running arbitrary cmd
    && dotnet help

ENV \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1 \
    CXX=clang++ \
    CC=clang

FROM base as builder

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore

WORKDIR /project

FROM base as tester

# Install ASP.NET Core runtimes using install script
# There is no arm64 runtime available for .NET Core 2.1, so just install the .NET Core runtime in that case

RUN if [ "$(uname -m)" = "x86_64" ]; \
    then export NETCORERUNTIME2_1=aspnetcore; \
    else export NETCORERUNTIME2_1=dotnet; \
    fi \
    && curl -sSL https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --runtime $NETCORERUNTIME2_1 --channel 2.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 5.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 6.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 7.0 --install-dir /usr/share/dotnet --no-path \
    && rm dotnet-install.sh


# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project
