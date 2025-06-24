﻿FROM andrewlockdd/alpine-clang:1.0 AS base
ARG DOTNETSDK_VERSION

ENV \
    # Unset ASPNETCORE_URLS from aspnet base image
    ASPNETCORE_URLS= \
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
    # SDK version
    DOTNET_SDK_VERSION=$DOTNETSDK_VERSION \
    # Disable the invariant mode (set in base image)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # Disable LTTng tracing with QUIC
    QUIC_LTTng=0 \
    # https://github.com/dotnet/runtime/issues/13648
    DOTNET_EnableWriteXorExecute=0
    
RUN apk update \
        && apk upgrade \
        && apk add --no-cache \
        ca-certificates \
        \
        # .NET Core dependencies
        krb5-libs \
        libgcc \
        libintl \
        libssl1.1 \
        libstdc++ \
        zlib-dev \
        \
        # SDK dependencies
        curl \
        icu-libs \
        \
        # our dependencies
        cmake \
        git \
        bash \
        make \
        alpine-sdk \
        util-linux-dev \
        autoconf \
        libtool \
        automake \
        xz-dev \
        gdb \
        musl-dbg \
        cppcheck \
        build-base \
        libldap \
        lldb \
        py3-lldb \
        # Download and install nfpm
    && apkArch="$(apk --print-arch)" \
    && wget https://github.com/goreleaser/nfpm/releases/download/v2.39.0/nfpm_2.39.0_${apkArch}.apk \
    && apk add --allow-untrusted nfpm_2.39.0_${apkArch}.apk \
    && rm nfpm_2.39.0_${apkArch}.apk

ENV IsAlpine=true \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1

# Install the .NET SDK
RUN curl -sSL https://github.com/dotnet/install-scripts/raw/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && dotnet help

# Install .NET Core runtimes using install script (don't install 2.1 on ARM64, because it's not available)
RUN if [ "$(uname -m)" != "aarch64" ]; then \
        ./dotnet-install.sh --runtime aspnetcore --channel 2.1 --install-dir /usr/share/dotnet --no-path; \
    fi \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 5.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 6.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 7.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 8.0 --install-dir /usr/share/dotnet --no-path \
    && rm dotnet-install.sh

ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-counters
RUN dotnet tool install --global dotnet-coverage
RUN dotnet tool install --global dotnet-dump
RUN dotnet tool install --global dotnet-gcdump
RUN dotnet tool install --global dotnet-monitor
RUN dotnet tool install --global dotnet-trace
RUN dotnet tool install --global dotnet-stack
RUN dotnet tool install --global dotnet-symbol
RUN dotnet tool install --global dotnet-debugger-extensions
RUN dotnet tool install --global dotnet-sos
RUN dotnet tool install --global dotnet-dsrouter
RUN dotnet sos install &&\
    dotnet debugger-extensions install --accept-license-agreement

WORKDIR /project
