# Approach A of docs/development/rfc-linux-x64-build-host.md: a plain, current Ubuntu LTS
# build host for the x86_64 Linux Datadog.Tracer.Native / Datadog.Profiler.Native build,
# replacing the real CentOS 7 container (centos7.dockerfile/centos7.build.dockerfile) that
# task requires today. The glibc-2.17 floor is enforced at link time via
# build/cmake/Glibc217.cmake.x86_64 plus a frozen, harvested sysroot (see
# glibc217-sysroot.harvest.dockerfile) - not by this image's own glibc version. Unlike
# centos7.build.dockerfile, there is no from-source LLVM/cmake bootstrap here: Ubuntu already
# ships a new enough cmake, and clang comes from the stock apt.llvm.org packages (the same
# recipe debian.dockerfile already uses to compile this exact native source for
# cppcheck/clang-tidy passes).
FROM ubuntu:22.04 AS base

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
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # Disable LTTng tracing with QUIC
    QUIC_LTTng=0

# nfpm (below) isn't in Ubuntu's own repos - it needs GoReleaser's dedicated, HTTPS-only apt
# repo registered first, same as debian.dockerfile does. Missing this registration is what
# broke the first CI run of this Dockerfile ("E: Unable to locate package nfpm"). Registering
# it and installing ca-certificates in the SAME apt-get install doesn't work either
# (verified) - a fresh ubuntu:22.04 has no CA bundle at all, so apt can't validate the
# HTTPS repo's certificate until ca-certificates is already installed. Hence two passes:
# ca-certificates first via Ubuntu's own (plain HTTP) mirrors, then add the goreleaser repo
# and update again before installing everything else.
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ca-certificates \
    && echo 'deb [trusted=yes] https://repo.goreleaser.com/apt/ /' | tee /etc/apt/sources.list.d/goreleaser.list \
    && apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        git \
        procps \
        wget \
        curl \
        unzip \
        cmake \
        make \
        gcc \
        build-essential \
        rpm \
        uuid-dev \
        autoconf \
        automake \
        libtool \
        liblzma-dev \
        gdb \
        libicu-dev \
        zlib1g-dev \
        cppcheck \
        # required to install clang
        lsb-release \
        software-properties-common \
        gnupg \
        nfpm \
    && rm -rf /var/lib/apt/lists/*

# Install Clang - stock apt.llvm.org packages, no from-source build needed (unlike
# centos7.build.dockerfile, which has to bootstrap its own compiler because CentOS 7 has
# nothing new enough to build a modern clang with).
RUN wget https://apt.llvm.org/llvm.sh \
    && chmod u+x llvm.sh \
    && ./llvm.sh 16 all \
    && rm llvm.sh \
    && ln -s `which clang-16` /usr/bin/clang \
    && ln -s `which clang++-16` /usr/bin/clang++ \
    && ln -s `which clang-tidy-16` /usr/bin/clang-tidy \
    && ln -s `which run-clang-tidy-16` /usr/bin/run-clang-tidy

# Fetch and verify the frozen glibc-2.17 sysroot harvested once, offline, from a real
# CentOS 7 container (see glibc217-sysroot.harvest.dockerfile) - the only piece of CentOS 7
# that survives into this image is a handful of old .so's and headers, not a whole container.
#
# TODO(stage-0 spike): replace REPLACE_WITH_PINNED_SHA512 with the real sha512sum of the
# tarball once glibc217-sysroot.harvest.dockerfile has actually been run and the tarball
# uploaded to apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/.
RUN curl -sSL https://apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/glibc217-sysroot-x86_64.tar.gz --output glibc217-sysroot-x86_64.tar.gz \
    && echo 'REPLACE_WITH_PINNED_SHA512  glibc217-sysroot-x86_64.tar.gz' | sha512sum --check \
    && mkdir -p /sysroot/x86_64-glibc217 \
    && tar -xzf glibc217-sysroot-x86_64.tar.gz -C /sysroot/x86_64-glibc217 \
    && rm glibc217-sysroot-x86_64.tar.gz

# Install the .NET SDK
RUN curl -sSL https://github.com/dotnet/install-scripts/raw/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && rm ./dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
# Trigger first run experience by running arbitrary cmd
    && dotnet help

ENV \
    DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1 \
    CXX=clang++ \
    CC=clang \
    UseGlibc217Sysroot=true

FROM base AS builder

ENV USE_NATIVE_SDK_VERSION=true

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project
