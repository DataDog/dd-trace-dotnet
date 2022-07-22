FROM centos:7 as base

RUN yum update -y \
    && yum install -y git \
      gcc \
      gcc-c++ \
      make \
      python \
      zlib \
      zlib-devel \
      wget \
      libcurl \
      libcurl-devel

#install cmake version
RUN wget https://cmake.org/files/v3.13/cmake-3.13.4.tar.gz && \
    tar zxvf cmake-3.* && \
    cd cmake-3.* && \
    ./bootstrap --system-curl && \
    make -j$(nproc) && \
    make install && \
    ln -s /usr/local/bin/cmake /usr/bin/cmake

RUN cd .. && \
    rm -rf cmake-3.*

# build and install llvm/clang
RUN git clone --depth 1 --branch release/9.x https://github.com/llvm/llvm-project.git && \
    cd llvm-project && \
    mkdir build && \
    cd build && \
    cmake -DLLVM_ENABLE_PROJECTS="clang;clang-tools-extra" -DCMAKE_BUILD_TYPE=Release -DLLVM_TEMPORARILY_ALLOW_OLD_TOOLCHAIN=1 -G "Unix Makefiles" ../llvm && \
    make -j$(nproc) && \
    make install

RUN cd ../.. && \
    rm -rf llvm-project


