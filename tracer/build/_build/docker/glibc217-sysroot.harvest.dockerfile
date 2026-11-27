# syntax=docker/dockerfile:1.6
#
# Harvests a glibc-2.17 sysroot from CentOS 7 (x86_64 or aarch64). Never pushed to a registry.
#
# Usage, x86_64 (DEVTOOLSET_VERSION defaults to 11):
#   docker build --platform linux/amd64 --target harvest -o type=local,dest=./glibc217-sysroot-out \
#       -f tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile \
#       tracer/build/_build/docker
#   tar -czf glibc217-sysroot-x86_64.tar.gz -C ./glibc217-sysroot-out .
#   sha512sum glibc217-sysroot-x86_64.tar.gz
#   # upload tarball + hash to apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/
#
# Usage, aarch64 (devtoolset-11 was never released for aarch64; uses 10 instead.
# On x86_64 dev machines: `docker run --privileged --rm tonistiigi/binfmt --install arm64`):
#   docker build --platform linux/arm64 --build-arg DEVTOOLSET_VERSION=10 \
#       --target harvest -o type=local,dest=./glibc217-sysroot-out-aarch64 \
#       -f tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile \
#       tracer/build/_build/docker
#   tar -czf glibc217-sysroot-aarch64.tar.gz -C ./glibc217-sysroot-out-aarch64 .
#   sha512sum glibc217-sysroot-aarch64.tar.gz
#   # upload tarball + hash to apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/
#
# Without BuildKit -o/--output:
#   docker build --platform <platform> --target harvest -t glibc217-harvest:local -f <this file> <context>
#   docker create --name tmp glibc217-harvest:local
#   docker cp tmp:/sysroot ./glibc217-sysroot-out && docker rm tmp

ARG DEVTOOLSET_VERSION=11

FROM --platform=$TARGETPLATFORM centos:7 AS base
ARG DEVTOOLSET_VERSION

# replace the centos repository with vault.centos.org because they shut down the original
RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # glibc-devel provides linker scripts + *_nonshared.a that the plain glibc package doesn't ship
    && yum install -y glibc glibc-devel glibc-headers \
    # centos-release-scl adds a new repo file pointing at dead mirrors, so repeat the fixup
    && yum install -y centos-release-scl \
    # On aarch64, CentOS's SCLo channel was archived under a separate "altarch" tree at a
    # specific point release, not under centos/7/ the way x86_64 is (CentOS-SCLo-scl-rh.repo's
    # baseurl is the literal string ".../centos/7/sclo/$basearch/rh/" - confirmed reachable at
    # http://vault.centos.org/altarch/7.9.2009/sclo/aarch64/rh/, unreachable at .../centos/7/...).
    && if [ "$(uname -m)" = "aarch64" ]; then \
           sed -i 's|/centos/7/sclo|/altarch/7.9.2009/sclo|g' /etc/yum.repos.d/*.repo; \
       fi \
    && sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # devtoolset-$DEVTOOLSET_VERSION provides libstdc++.a/libgcc.a and C++ headers built
    # against glibc 2.17; a modern host's copies reference symbols newer than 2.17
    && yum install -y devtoolset-${DEVTOOLSET_VERSION}-gcc-c++

# Stage only the link-time glibc files (not the whole /lib64 or /usr/lib64 trees).
# C headers are intentionally not harvested. The dynamic linker SONAME is arch-specific.
RUN set -eux; \
    mkdir -p /harvest/lib64 /harvest/usr/lib64; \
    ARCH="$(uname -m)"; \
    case "$ARCH" in \
        x86_64) INTERP=ld-linux-x86-64.so.2 ;; \
        aarch64) INTERP=ld-linux-aarch64.so.1 ;; \
        *) echo "Unsupported architecture: $ARCH" >&2; exit 1 ;; \
    esac; \
    for f in $INTERP ld-2.17.so \
             libc.so.6 libc-2.17.so libc.so \
             libpthread.so.0 libpthread-2.17.so libpthread.so libpthread_nonshared.a \
             libdl.so.2 libdl-2.17.so \
             libm.so.6 libm-2.17.so \
             librt.so.1 librt-2.17.so \
             libresolv.so.2 libresolv-2.17.so \
             libnsl.so.1 libnsl-2.17.so \
             libutil.so.1 libutil-2.17.so \
             libcrypt.so.1 libcrypt-2.17.so; do \
        cp -a /lib64/$f /harvest/lib64/; \
    done; \
    for f in libc.so libc_nonshared.a \
             libpthread.so libpthread_nonshared.a \
             libdl.so libm.so librt.so libresolv.so libnsl.so libutil.so libcrypt.so \
             crt1.o crti.o crtn.o Scrt1.o gcrt1.o Mcrt1.o; do \
        cp -a /usr/lib64/$f /harvest/usr/lib64/; \
    done; \
    # On aarch64, libc.so's linker script GROUP() references the interpreter at
    # /lib/ld-linux-aarch64.so.1 (not /lib64/...) even though the real file - like
    # everything else here - lives in lib64. Make --sysroot resolve that path too.
    # x86_64 doesn't need this: its GROUP() references /lib64/ld-linux-x86-64.so.2 directly.
    if [ "$ARCH" = "aarch64" ]; then ln -s lib64 /harvest/lib; fi; \
    # devtoolset libstdc++/libgcc archives and C++ headers. Output folder name
    # (devtoolset11, devtoolset10, ...) must match what Glibc217.cmake.* expects.
    DT_TRIPLE="${ARCH}-redhat-linux"; \
    DT_LIB=/opt/rh/devtoolset-${DEVTOOLSET_VERSION}/root/usr/lib/gcc/${DT_TRIPLE}/${DEVTOOLSET_VERSION}; \
    DT_INC=/opt/rh/devtoolset-${DEVTOOLSET_VERSION}/root/usr/include/c++/${DEVTOOLSET_VERSION}; \
    DT_OUT=/harvest/devtoolset${DEVTOOLSET_VERSION}; \
    mkdir -p $DT_OUT/lib $DT_OUT/include; \
    cp -a $DT_LIB/libstdc++.a $DT_LIB/libstdc++.so $DT_LIB/libgcc.a $DT_LIB/libgcc_s.so $DT_LIB/libgcc_eh.a $DT_OUT/lib/; \
    cp -a $DT_INC $DT_OUT/include/${DEVTOOLSET_VERSION}

FROM scratch AS harvest
# Export a flat layout so `docker build -o type=local,dest=<dir>` produces the structure
# ubuntu.dockerfile's extraction step expects.
COPY --from=base /harvest/ /
