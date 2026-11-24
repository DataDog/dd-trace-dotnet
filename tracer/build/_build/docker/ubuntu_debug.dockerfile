# Debug image for opening core dumps (lldb, dotnet-dump, dotnet-symbol, dotnet-sos).
# Based on Ubuntu 22.04 like ubuntu.dockerfile. Used by tools/dumps/start_debian-arm64.{sh,cmd}.
FROM ubuntu:22.04 AS base
ARG DOTNETSDK_VERSION

ENV \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=1 \
    # We build the images ahead of time, so the first-time experience, which should speed up subsequent execution, is run at VM build time
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=0 \
    # Disable telemetry to reduce overhead
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    # Disable the SDK from picking up a global install
    DOTNET_MULTILEVEL_LOOKUP=0 \
    # Set CLI language to English for consistent logs
    DOTNET_CLI_UI_LANGUAGE="en" \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # Disable LTTng tracing with QUIC
    QUIC_LTTng=0 \
    # https://github.com/dotnet/runtime/issues/13648
    DOTNET_EnableWriteXorExecute=0

# Add nfpm source
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ca-certificates curl wget gnupg \
    && echo 'deb [trusted=yes] https://repo.goreleaser.com/apt/ /' | tee /etc/apt/sources.list.d/goreleaser.list \
    && apt-get update \
    && apt-get -y upgrade \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --fix-missing \
        git \
        procps \
        cmake \
        make \
        gcc \
        build-essential \
        rpm \
        uuid-dev \
        autoconf \
        libtool \
        liblzma-dev \
        gdb \
        cppcheck \
        zlib1g-dev \
        # .NET runtime dependencies
        libicu70 \
        libssl3 \
        # required to install clang
        lsb-release \
        software-properties-common \
        unzip \
        nfpm \
        lldb \
    && rm -rf /var/lib/apt/lists/*

# Install Clang
RUN wget https://apt.llvm.org/llvm.sh && \
    chmod u+x llvm.sh && \
    ./llvm.sh 16 all && \
    ln -s `which clang-16` /usr/bin/clang && \
    ln -s `which clang++-16` /usr/bin/clang++ && \
    ln -s `which clang-tidy-16` /usr/bin/clang-tidy && \
    ln -s `which run-clang-tidy-16` /usr/bin/run-clang-tidy && \
    # llvm.sh installs lldb-16, which makes apt remove Ubuntu's lldb package, so only the versioned binary is left
    ln -s `which lldb-16` /usr/bin/lldb

# Install the .NET SDK
RUN curl -sSL https://github.com/dotnet/install-scripts/raw/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.sh --output dotnet-install.sh  \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
# Trigger first run experience by running arbitrary cmd
    && dotnet help

ENV \
    DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1 \
    CXX=clang++ \
    CC=clang

# Install ASP.NET Core runtimes using install script
# There is no arm64 runtime available for .NET Core 2.1, so just install the .NET Core runtime in that case
RUN if [ "$(uname -m)" = "x86_64" ]; \
    then export NETCORERUNTIME2_1=aspnetcore; \
    else export NETCORERUNTIME2_1=dotnet; \
    fi \
    && ./dotnet-install.sh --runtime $NETCORERUNTIME2_1 --channel 2.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 5.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 6.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 7.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 8.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 9.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 10.0 --install-dir /usr/share/dotnet --no-path \
    && rm dotnet-install.sh

ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-counters
RUN dotnet tool install --global dotnet-coverage
RUN dotnet tool install --global dotnet-dump
RUN dotnet tool install --global dotnet-gcdump
# dotnet-monitor 10.x can't be installed with the .NET 9 SDK used by the dump launchers
RUN dotnet tool install --global dotnet-monitor --version "9.*"
RUN dotnet tool install --global dotnet-trace
RUN dotnet tool install --global dotnet-stack
RUN dotnet tool install --global dotnet-symbol
RUN dotnet tool install --global dotnet-debugger-extensions
RUN dotnet tool install --global dotnet-sos
RUN dotnet tool install --global dotnet-dsrouter
RUN dotnet sos install &&\
    dotnet debugger-extensions install --accept-license-agreement

WORKDIR /project
