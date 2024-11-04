FROM gleocadie/centos7-clang16 as base

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

# replace the centos repository with vault.centos.org because they shut down the original
RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    && printf '[goreleaser]\nname=GoReleaser\nbaseurl=https://repo.goreleaser.com/yum/\nenabled=1\ngpgcheck=0' | tee /etc/yum.repos.d/goreleaser.repo \
    && yum update -y \
    && yum install -y centos-release-scl \
    && yum install -y\
        git \
        procps \
        wget \
        curl \
        gcc \
        build-essential \
        rpm \
        uuid-dev \
        autoconf \
        libtool \
        libstdc++ \
        libstdc++-devel \
        liblzma-dev \
        gdb \
        libicu \
        which \
        zlib \
        zlib-devel \
        devtoolset-7 \
        rpm-build \
        expect \
        sudo \
        gawk \
        libasan6 \
        libubsan1 \
        nfpm

RUN curl -Ol https://raw.githubusercontent.com/llvm-mirror/clang-tools-extra/master/clang-tidy/tool/run-clang-tidy.py \
    && mv run-clang-tidy.py /usr/bin/ \
    && chmod +x /usr/bin/run-clang-tidy.py \ 
    && ln -s /usr/bin/run-clang-tidy.py /usr/bin/run-clang-tidy

# Install CppCheck
RUN curl -sSL https://apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/cppcheck-2.7-1.el7.x86_64.rpm --output cppcheck-2.7-1.el7.x86_64.rpm \
    && echo '7b1e2c6abf34cfbc9d542ea466f2fb752ec2cee2ef92297271c8c8325cf8d16e29d1c32784da0ccc6bc4e9bee8647b14daa8568f4e2d1fd1626a584dc4f2419a cppcheck-2.7-1.el7.x86_64.rpm' | sha512sum --check \
    && sudo yum localinstall -y cppcheck-2.7-1.el7.x86_64.rpm

# Install the .NET SDK
COPY ./bootstrap/dotnet-install.sh .
RUN ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && rm ./dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
# Trigger first run experience by running arbitrary cmd
    && dotnet help

ENV \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1 \
    CXX=clang++ \
    CC=clang

FROM base as builder

ENV USE_NATIVE_SDK_VERSION=true

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project

FROM base as tester

# Install ASP.NET Core runtimes using install script
# There is no arm64 runtime available for .NET Core 2.1, so just install the .NET Core runtime in that case

COPY ./bootstrap/dotnet-install.sh .
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
    && rm dotnet-install.sh


ENV USE_NATIVE_SDK_VERSION=true

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project
