# Approach A of docs/development/rfc-linux-x64-build-host.md: a plain, current Ubuntu LTS
# build host for the x86_64 Linux Datadog.Tracer.Native / Datadog.Profiler.Native build,
# replacing the real CentOS 7 container (centos7.dockerfile/centos7.build.dockerfile) that
# task requires today. The glibc-2.17 floor is enforced at link time via
# build/cmake/Glibc217.cmake.x86_64 plus a frozen, harvested sysroot - not by this image's
# own glibc version. Unlike centos7.build.dockerfile, there is no from-source LLVM/cmake
# bootstrap here: Ubuntu already ships a new enough cmake, and clang comes from the stock
# apt.llvm.org packages (the same recipe debian.dockerfile already uses to compile this exact
# native source for cppcheck/clang-tidy passes).
#
# The glibc-2.17 sysroot is harvested INLINE below (glibc217-centos/glibc217-harvest stages),
# not fetched from external blob storage - this keeps the whole image self-contained and
# testable in CI with no upload/hosting step required first. This is functionally identical
# to (and kept in sync with) glibc217-sysroot.harvest.dockerfile, which exists separately as
# a standalone, inspectable reference and as the basis for a leaner, blob-storage-hosted
# variant later if the per-build `yum install` here (fast in practice - ~16s with no
# emulation tax on real x64 CI hardware) ever becomes worth trading for a one-time upload.
FROM --platform=linux/amd64 centos:7 AS glibc217-centos

RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # glibc-devel pulls in glibc-headers too, and (critically) the /usr/lib64/*.so linker
    # scripts + *_nonshared.a archives that the plain `glibc` package doesn't ship - without
    # these, -lc simply fails to resolve at link time. No compiler toolchain is installed
    # here for COMPILING (the build host compiles with its own modern headers - see
    # Glibc217.cmake.x86_64's comment for why that's safe for C); this image only needs to
    # supply old glibc's own files for that part.
    && yum install -y glibc glibc-devel glibc-headers \
    # centos-release-scl adds ITS OWN new repo file (centos-sclo-rh) pointing at the same
    # dead mirror.centos.org - the fixup above ran before that file existed, so it has to be
    # repeated here or the subsequent devtoolset-11 install fails with "Cannot find a valid
    # baseurl for repo: centos-sclo-rh/x86_64" (verified).
    && yum install -y centos-release-scl \
    && sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # devtoolset-11 is needed for a DIFFERENT reason: -static-libstdc++/-static-libgcc pull
    # in libstdc++.a/libgcc.a from wherever clang auto-detects a GCC install, and a build
    # host's OWN (modern) GCC's libstdc++.a internally calls glibc functions newer than 2.17
    # (__libc_single_threaded, __cxa_thread_atexit_impl, pthread_cond_clockwait - verified in
    # CI: "undefined symbol" for all three, referenced from libstdc++.a and from inline
    # header code compiled against modern C++ headers). devtoolset-11 is Red Hat's own GCC
    # 11 rebuilt specifically to still target RHEL7's glibc floor - same GCC major version as
    # a typical modern host's system GCC, but built to not assume anything newer than glibc
    # 2.17 is present. It's also the newest devtoolset CentOS 7 ever shipped (there is no
    # devtoolset-12) and is already a dependency of this pipeline elsewhere
    # (centos7.build.dockerfile uses it to bootstrap clang-16). Both its libstdc++.a/libgcc.a
    # AND its C++ headers are needed - the glibc-version-specific implementation of
    # functions like std::condition_variable::wait_until is header-inlined, so using a
    # modern host's C++ headers reintroduces the same problem even with old libraries linked.
    && yum install -y devtoolset-11-gcc-c++

# Stage only the specific glibc files actually needed at link time - deliberately NOT the
# whole /lib64 or /usr/lib64 trees, which also contain unrelated packages baked into the
# centos:7 base image (libcrypto, libcryptsetup, ...), some with permission bits that choke
# BuildKit's local-directory export (e.g. pm-utils/module.d). /usr/include (glibc's own C
# headers) is intentionally not harvested: Glibc217.cmake.x86_64 never points header search
# at this sysroot for C, so they'd be dead weight. File list verified against a real
# x86_64 centos:7 + glibc-devel container.
RUN set -eux; \
    mkdir -p /harvest/lib64 /harvest/usr/lib64; \
    for f in ld-linux-x86-64.so.2 ld-2.17.so \
             libc.so.6 libc-2.17.so libc.so \
             libpthread.so.0 libpthread-2.17.so libpthread.so libpthread_nonshared.a \
             libdl.so.2 libdl-2.17.so \
             libm.so.6 libm-2.17.so \
             librt.so.1 librt-2.17.so \
             libresolv.so.2 libresolv-2.17.so \
             libnsl.so.1 libnsl-2.17.so \
             libutil.so.1 libutil-2.17.so \
             libcrypt.so.1 libcrypt-2.17.so; do \
        cp -a /lib64/$f /harvest/lib64/; \
    done; \
    for f in libc.so libc_nonshared.a \
             libpthread.so libpthread_nonshared.a \
             libdl.so libm.so librt.so libresolv.so libnsl.so libutil.so libcrypt.so \
             crt1.o crti.o crtn.o Scrt1.o gcrt1.o Mcrt1.o; do \
        cp -a /usr/lib64/$f /harvest/usr/lib64/; \
    done; \
    # devtoolset-11's libstdc++/libgcc (both the static archives -static-libstdc++/
    # -static-libgcc need, and the small .so linker-script stubs) plus its C++ headers
    # (main + the arch-specific subdir, both required - the latter has bits/c++config.h).
    DT11_LIB=/opt/rh/devtoolset-11/root/usr/lib/gcc/x86_64-redhat-linux/11; \
    DT11_INC=/opt/rh/devtoolset-11/root/usr/include/c++/11; \
    mkdir -p /harvest/devtoolset11/lib /harvest/devtoolset11/include; \
    cp -a $DT11_LIB/libstdc++.a $DT11_LIB/libstdc++.so $DT11_LIB/libgcc.a $DT11_LIB/libgcc_s.so $DT11_LIB/libgcc_eh.a /harvest/devtoolset11/lib/; \
    cp -a $DT11_INC /harvest/devtoolset11/include/11

FROM scratch AS glibc217-harvest
COPY --from=glibc217-centos /harvest/ /

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

# The frozen glibc-2.17 sysroot, harvested inline above - no external fetch/hash-check
# needed. lib64/ and usr/lib64/ land directly under /sysroot/x86_64-glibc217, matching what
# build/cmake/Glibc217.cmake.x86_64 expects.
COPY --from=glibc217-harvest / /sysroot/x86_64-glibc217/

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
