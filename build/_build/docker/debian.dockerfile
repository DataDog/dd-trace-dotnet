ARG DOTNETSDK_VERSION
# debian 10 image
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-buster-slim as base
# ubuntu image
# FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-focal

RUN apt-get update \
    && apt-get -y upgrade \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --fix-missing \
        git \
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
    && gem install --no-document fpm


ENV CXX=clang++
ENV CC=clang

FROM base as builder

# Copy the build project in and build it
COPY . /build
RUN dotnet build /build
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
    && rm dotnet-install.sh


# Copy the build project in and build it
COPY . /build
RUN dotnet build /build
WORKDIR /project
