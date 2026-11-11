# syntax=docker/dockerfile:1.6

FROM centos:7 as base

# replace the centos repository with vault.centos.org because they shut down the original
RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    && yum update -y \
    && yum install -y centos-release-scl \
    # centos-release-scl adds SCLo repos that still point at the retired mirrorlist.centos.org: fix them too
    && sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/CentOS-SCLo-*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/CentOS-SCLo-*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/CentOS-SCLo-*.repo \
    && yum install -y git \
      gcc \
      gcc-c++ \
      make \
      python \
      zlib \
      zlib-devel \
      wget \
      libcurl \
      libcurl-devel \
      devtoolset-11 \
      python3

# centos 7 is the glibc 2.17 baseline for linux-x64 (tracer/build/artifacts/requirements.json) and is EOL, so no
# fixed ca-certificates package will ship. Runs after the yum update above, which would otherwise restore them:
# remove the distrusted eMudhra roots "emSign ECC Root CA - G3" / "AAA Certificate Services" (IR-62343) from the
# p11-kit source bundle, re-extract every derived store and make NSS read the p11-kit trust store.
RUN set -eux; \
    src=/usr/share/pki/ca-trust-source/ca-bundle.trust.p11-kit; \
    awk 'function flush() { if (!drop) printf "%s", buf; buf = ""; drop = 0 } \
         /^\[p11-kit-object-v1\]/ { flush() } \
         /^label: "(Comodo AAA Services root|AAA Certificate Services|emSign ECC Root CA - G3)"$/ { drop = 1 } \
         { buf = buf $0 "\n" } \
         END { flush() }' "$src" > /tmp/ca-bundle.trust.p11-kit; \
    mv /tmp/ca-bundle.trust.p11-kit "$src"; \
    update-ca-trust extract; \
    ln -sf /usr/lib64/pkcs11/p11-kit-trust.so /usr/lib64/nss/libnssckbi.so

FROM base AS build-cmake

# build cmake version
RUN wget https://cmake.org/files/v3.13/cmake-3.13.4.tar.gz && \
    tar zxvf cmake-3.13.4.tar.gz

RUN cd cmake-3.13.4 && \
    ./bootstrap --system-curl && \
    make -j$(nproc)

FROM base AS intermediate
RUN --mount=target=/cmake-3.13.4,from=build-cmake,source=cmake-3.13.4,rw \
    cd /cmake-3.13.4 && \
    make install && \
    ln -s /usr/local/bin/cmake /usr/bin/cmake

FROM intermediate AS build-llvm-clang

# build llvm/clang
RUN source scl_source enable devtoolset-11 && \
    \
    # clone llvm/clang repo
    git clone --depth 1 --branch llvmorg-16.0.6 https://github.com/llvm/llvm-project.git && \
    \
    # setup build folder
    cd llvm-project && \
    mkdir build && \
    cd build && \
    \
    # build llvm/clang + extra tool
    cmake -DLLVM_ENABLE_PROJECTS="clang;clang-tools-extra;compiler-rt;lld" -DCMAKE_BUILD_TYPE=Release -DLLVM_TEMPORARILY_ALLOW_OLD_TOOLCHAIN=1 -G "Unix Makefiles" ../llvm && \
    make -j$(nproc)

FROM intermediate as final
RUN --mount=target=/llvm-project,from=build-llvm-clang,source=llvm-project,rw \
    cd /llvm-project/build && \
    make install


