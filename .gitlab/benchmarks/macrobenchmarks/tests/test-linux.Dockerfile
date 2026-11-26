FROM debian:bookworm-slim

ENV PYTHONDONTWRITEBYTECODE=1

RUN apt-get update \
    && apt-get -y install --no-install-recommends \
        bash \
        coreutils \
        curl \
        git \
        jq \
        python3 \
        unzip \
    && apt-get -y clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /repo
