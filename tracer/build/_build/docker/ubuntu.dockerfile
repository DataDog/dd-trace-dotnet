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

# Install ca-certificates first (fresh ubuntu:22.04 has no CA bundle), then register
# GoReleaser's HTTPS apt repo for nfpm before installing everything else.
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

# Install Clang
RUN wget https://apt.llvm.org/llvm.sh \
    && chmod u+x llvm.sh \
    && ./llvm.sh 16 all \
    && rm llvm.sh \
    && ln -s `which clang-16` /usr/bin/clang \
    && ln -s `which clang++-16` /usr/bin/clang++ \
    && ln -s `which clang-tidy-16` /usr/bin/clang-tidy \
    && ln -s `which run-clang-tidy-16` /usr/bin/run-clang-tidy

# Fetch and verify the frozen glibc-2.17 sysroot (architecture-specific).
# See glibc217-sysroot.harvest.dockerfile (single file, both arches).
RUN set -eux; \
    ARCH="$(uname -m)"; \
    case "$ARCH" in \
        x86_64) SYSROOT_SHA512='2e891242b066fe3c7d0c95cc68412a24b4f80bb8ae9d92226877a5c5b84226141425d70e920eeefe5205655f9669de7bb77f529089c72332f3a656fdbc72cc30' ;; \
        aarch64) SYSROOT_SHA512='10d951f73e9e430d93af9510ebc82eb9a63ccb39c4edb06be8cb961565a1ff3414617d5013f785261350ef713fb5a4824055ee889bce4b556e418056388fb983' ;; \
        *) echo "Unsupported architecture: $ARCH" >&2; exit 1 ;; \
    esac \
    && curl -sSL https://apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/glibc217-sysroot-${ARCH}.tar.gz --output glibc217-sysroot.tar.gz \
    && echo "${SYSROOT_SHA512}  glibc217-sysroot.tar.gz" | sha512sum --check \
    && mkdir -p /sysroot/${ARCH}-glibc217 \
    && tar -xzf glibc217-sysroot.tar.gz -C /sysroot/${ARCH}-glibc217 \
    && rm glibc217-sysroot.tar.gz

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

# TODO: not sure if we need this anymore
ENV USE_NATIVE_SDK_VERSION=true

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project
