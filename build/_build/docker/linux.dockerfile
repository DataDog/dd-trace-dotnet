ARG DOTNETSDK_VERSION
#FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-buster-slim
# debian 10 image
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-focal


RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
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
        apt-transport-https \
        aspnetcore-runtime-2.1 \
        aspnetcore-runtime-3.0 \
        aspnetcore-runtime-3.1 \
    && gem install --no-document fpm

RUN apt-get install -y aspnetcore-runtime-3.0

ENV CXX=clang++
ENV CC=clang

WORKDIR /project