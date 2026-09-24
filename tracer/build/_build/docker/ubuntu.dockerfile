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
        # TEMPORARY diagnostic (see OpenLdapTests.CheckOpenLdapCrash): trace syscalls/signals
        strace \
        # required to install clang
        lsb-release \
        software-properties-common \
        gnupg \
        nfpm \
    && rm -rf /var/lib/apt/lists/*

# libssl1.1 for .NET Core 3.1 and older. Jammy ships OpenSSL 3 and dropped the package
# from its repos, so install the .deb from the Ubuntu security/ports pool.
RUN set -eux; \
    ARCH="$(dpkg --print-architecture)"; \
    case "$ARCH" in \
        amd64) LIBSSL_URL=http://security.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb; \
               LIBSSL_SHA256=7cf39d70a639017d1dd7c8d36daa2258063608688e449fddf40ffdd46f992a78 ;; \
        arm64) LIBSSL_URL=http://ports.ubuntu.com/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2.24_arm64.deb; \
               LIBSSL_SHA256=dded4572af8b0a9e0310909f211a519cc6409fda31ea81132a77e268b0ec0f2f ;; \
        *) echo "Unsupported architecture: $ARCH" >&2; exit 1 ;; \
    esac; \
    curl -sSL "$LIBSSL_URL" --output libssl1.1.deb; \
    echo "${LIBSSL_SHA256}  libssl1.1.deb" | sha256sum --check; \
    dpkg -i libssl1.1.deb; \
    rm libssl1.1.deb

# System.DirectoryServices.Protocols (LDAP client) dlopens libldap-2.4.so.2 by name; jammy
# ships OpenLDAP 2.5 (libldap-2.5.so.0) via curl's transitive dep instead. The client ABI for
# bind/search (what .NET's interop actually calls) stayed compatible across the SONAME bump,
# so a symlink is the standard workaround - see dotnet/runtime#69456.
RUN MULTIARCH="$(dpkg-architecture -qDEB_HOST_MULTIARCH)" \
    && ln -s "/usr/lib/$MULTIARCH/libldap-2.5.so.0" "/usr/lib/$MULTIARCH/libldap-2.4.so.2"

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
#
# For local iteration on the harvest itself, skip the fetch (SkipGlibc217SysrootFetch=true)
# and bind-mount your local harvest output at /sysroot/<arch>-glibc217 instead - avoids the
# harvest/tar/sha512sum/upload cycle entirely while you're still debugging the sysroot. e.g.:
#   docker build --platform linux/arm64 --build-arg SkipGlibc217SysrootFetch=true \
#       --target base -t ubuntu217-local -f tracer/build/_build/docker/ubuntu.dockerfile \
#       tracer/build/_build/docker
#   docker run --rm -it -v $(pwd)/glibc217-sysroot-out-aarch64:/sysroot/aarch64-glibc217 \
#       -v $(pwd):/project -w /project ubuntu217-local bash
# (targeting `base`, not `builder` - no need to build the Nuke project itself for this)
ARG SkipGlibc217SysrootFetch=false

RUN set -eux; \
    ARCH="$(uname -m)"; \
    mkdir -p /sysroot/${ARCH}-glibc217; \
    if [ "$SkipGlibc217SysrootFetch" = "true" ]; then \
        echo "Skipping glibc217 sysroot fetch - mount your local harvest output at /sysroot/${ARCH}-glibc217 when running this image."; \
    else \
        case "$ARCH" in \
            x86_64) SYSROOT_SHA512='2e891242b066fe3c7d0c95cc68412a24b4f80bb8ae9d92226877a5c5b84226141425d70e920eeefe5205655f9669de7bb77f529089c72332f3a656fdbc72cc30' ;; \
            aarch64) SYSROOT_SHA512='c1476e9afb0fd62b3ba19b1dbfcaa46a74a282b90354561bdf1cad24ff2398eb807f05945087473359830473691a26c6ca73855770c8227da682ebf1e4265aba' ;; \
            *) echo "Unsupported architecture: $ARCH" >&2; exit 1 ;; \
        esac; \
        curl -sSL https://apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/glibc217-sysroot-${ARCH}.tar.gz --output glibc217-sysroot.tar.gz; \
        echo "${SYSROOT_SHA512}  glibc217-sysroot.tar.gz" | sha512sum --check; \
        tar -xzf glibc217-sysroot.tar.gz -C /sysroot/${ARCH}-glibc217; \
        rm glibc217-sysroot.tar.gz; \
    fi

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

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project

FROM base AS tester

# Install ASP.NET Core runtimes using install script
# There is no arm64 runtime available for .NET Core 2.1, so just install the .NET Core runtime in that case
RUN if [ "$(uname -m)" = "x86_64" ]; \
    then export NETCORERUNTIME2_1=aspnetcore; \
    else export NETCORERUNTIME2_1=dotnet; \
    fi \
    && curl -sSL https://github.com/dotnet/install-scripts/raw/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --runtime $NETCORERUNTIME2_1 --channel 2.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 5.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 6.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 7.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 8.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 9.0 --install-dir /usr/share/dotnet --no-path \
    && rm dotnet-install.sh

ARG AZURE_FUNCTIONS_CORE_TOOLS_VERSION=4.11.0

RUN if [ "$(uname -m)" = "x86_64" ]; \
    then curl -fsSL "https://github.com/Azure/azure-functions-core-tools/releases/download/${AZURE_FUNCTIONS_CORE_TOOLS_VERSION}/Azure.Functions.Cli.linux-x64.${AZURE_FUNCTIONS_CORE_TOOLS_VERSION}.zip" --output azure-functions-core-tools.zip \
        && mkdir -p /opt/azure-functions-core-tools \
        && unzip -q azure-functions-core-tools.zip -d /opt/azure-functions-core-tools \
        && chmod +x /opt/azure-functions-core-tools/func \
        && ln -s /opt/azure-functions-core-tools/func /usr/local/bin/func \
        && rm azure-functions-core-tools.zip; \
    fi

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project
