FROM ubuntu:20.04

RUN apt-get update && \
    apt-get install -y \
        git \
        wget \
        curl \
        cmake \
        make \
        llvm \
        clang
