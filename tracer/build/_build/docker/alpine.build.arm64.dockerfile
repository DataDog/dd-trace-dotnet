# syntax=docker/dockerfile:1.6

FROM alpine:3.18 as base

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
        clang16 \
        clang16-extra-tools
