﻿ARG DOTNETSDK_VERSION
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-alpine3.14 as base

RUN apk update \
    && apk upgrade \
    && apk add --no-cache --update \
        clang \
        cmake \
        git \
        bash \
        make \
        alpine-sdk \
        ruby \
        ruby-dev \
        ruby-etc \
        util-linux-dev \
    && gem install --no-document fpm

ENV IsAlpine=true

FROM base as builder

# Copy the build project in and build it
WORKDIR /project
COPY . /build
RUN dotnet build /build

FROM base as tester

# Install .NET Core runtimes using install script
RUN curl -sSL https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --runtime aspnetcore --channel 2.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 5.0 --install-dir /usr/share/dotnet --no-path \
    && rm dotnet-install.sh

# Copy the build project in and build it
WORKDIR /project
COPY . /build
RUN dotnet build /build
