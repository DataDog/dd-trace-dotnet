FROM ubuntu:20.04

RUN apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y \
        git \
        wget \
        curl \
        cmake \
        make \
        llvm \
        clang \ 
        gcc
ENV CXX=clang++
ENV CC=clang

RUN ./build.sh CompileNativeSrcLinux --skip