FROM alpine:3.14 as alpine-base

RUN apk update \
        && apk upgrade \
        && apk add --no-cache \
        cmake \
        git \
        make \
        alpine-sdk \
        autoconf \
        libtool \
        automake \
        xz-dev \
        build-base \
        python3 \
        linux-headers \
        libexecinfo-dev

## snapshot 

FROM alpine-base AS build-llvm-clang

COPY ./docker/alpine_build_patch_llvm_clang.sh alpine_build_patch_llvm_clang.sh

# build and install llvm/clang
RUN git clone --depth 1 --branch llvmorg-16.0.6 https://github.com/llvm/llvm-project.git && \
    # download patches to apply
    chmod +x alpine_build_patch_llvm_clang.sh && \
    ./alpine_build_patch_llvm_clang.sh && \
    \
    # setup build folder
    cd llvm-project && \
    mkdir build && \
    cd build && \
    \
    # build llvm/clang
    cmake  -DCOMPILER_RT_BUILD_GWP_ASAN=OFF -DLLVM_ENABLE_PROJECTS="clang;clang-tools-extra;compiler-rt;lld" -DCMAKE_BUILD_TYPE=Release -DLLVM_TEMPORARILY_ALLOW_OLD_TOOLCHAIN=1 -DENABLE_LINKER_BUILD_ID=ON -DCLANG_VENDOR=Alpine -DLLVM_DEFAULT_TARGET_TRIPLE=x86_64-alpine-linux-musl -DLLVM_HOST_TRIPLE=x86_64-alpine-linux-musl  -G "Unix Makefiles" ../llvm && \
    make -j$(nproc)

FROM alpine-base as alpine-base-clang
RUN --mount=target=/llvm-project,from=build-llvm-clang,source=llvm-project,rw cd /llvm-project/build && make install

FROM alpine-base-clang as base
ARG DOTNETSDK_VERSION

ENV \
    # Unset ASPNETCORE_URLS from aspnet base image
    ASPNETCORE_URLS= \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=true \
    # SDK version
    DOTNET_SDK_VERSION=$DOTNETSDK_VERSION \
    # Disable the invariant mode (set in base image)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # Disable LTTng tracing with QUIC
    QUIC_LTTng=0
    
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
        ruby \
        ruby-dev \
        ruby-etc \
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
    && gem install --version 1.6.0 --user-install git \
    && gem install --version 2.7.6 dotenv \
    && gem install --version 1.14.2 --minimal-deps --no-document fpm

ENV IsAlpine=true \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1

# Install the .NET SDK
RUN curl -sSL https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && dotnet help

FROM base as builder

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project

FROM base as tester

# Install .NET Core runtimes using install script
RUN curl -sSL https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --runtime aspnetcore --channel 2.1 --install-dir /usr/share/dotnet --no-path \
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
