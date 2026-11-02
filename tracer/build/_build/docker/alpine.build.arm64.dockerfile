# syntax=docker/dockerfile:1.6

# See alpine.build.dockerfile for more information on how to build this image
FROM alpine:3.18 as base

# alpine 3.18 is the musl baseline for linux-musl-arm64 (tracer/build/artifacts/requirements.json) and is EOL,
# so no fixed ca-certificates package will ship: drop the distrusted eMudhra roots
# "emSign ECC Root CA - G3" / "AAA Certificate Services" (IR-62343) from the system trust store.
RUN apk add --no-cache --upgrade ca-certificates ca-certificates-bundle \
    && if [ -f /etc/ca-certificates.conf ]; then sed -i -e 's|^mozilla/emSign_ECC_Root_CA_-_G3.crt|!&|' -e 's|^mozilla/Comodo_AAA_Services_root.crt|!&|' /etc/ca-certificates.conf; fi \
    && rm -f /usr/share/ca-certificates/mozilla/emSign_ECC_Root_CA_-_G3.crt /usr/share/ca-certificates/mozilla/Comodo_AAA_Services_root.crt \
    && update-ca-certificates

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
