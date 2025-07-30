# syntax=docker/dockerfile:1.6

# Build this image as described in https://andrewlock.net/combining-multiple-docker-images-into-a-multi-arch-image
# Ultimately the process will look something like this, assuming that you need to build for both amd64 and arm64
# (if you're only changing one of the dockerfiles, you can use the existing manifest already on docker hub)
#
# cd tracer/build/_build/docker
# docker buildx build -t andrewlockdd/alpine-clang -f alpine.build.dockerfile --platform linux/amd64 --provenance false --output push-by-digest=true,type=image,push=true .
# docker buildx build -t andrewlockdd/alpine-clang -f alpine.build.arm64.dockerfile --platform linux/arm64 --provenance false --output push-by-digest=true,type=image,push=true .
#
# Create the manifest using the digests from the previous two commands and push it to Docker Hub, using an appropriate tag
# docker manifest create andrewlockdd/alpine-clang:dotnet10 --amend andrewlockdd/alpine-clang@sha256:5805da6408d5cbadee2be250b6ade63e1f2a6e4541146c3ec56d53d386eaff27 --amend andrewlockdd/alpine-clang@sha256:680c4e5a622100dd2b79d3f9da0a6434a150bd250714efcc1f109bce1bdd54e6
# docker manifest push andrewlockdd/alpine-clang:dotnet10
FROM alpine:3.17 as base

RUN apk update \
        && apk upgrade \
        && apk add --no-cache \
        cmake \
        bash \
        git \
        make \
        alpine-sdk \
        autoconf \
        libtool \
        automake \
        xz-dev \
        build-base \
        python3 \
        linux-headers

## snapshot 

FROM base AS build-llvm-clang

COPY alpine_build_patch_llvm_clang.sh alpine_build_patch_llvm_clang.sh

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

FROM base as final
RUN --mount=target=/llvm-project,from=build-llvm-clang,source=llvm-project,rw cd /llvm-project/build && make install