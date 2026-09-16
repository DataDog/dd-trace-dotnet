# syntax=docker/dockerfile:1.6
#
# One-time extraction tool that harvests a glibc-2.17 sysroot from CentOS 7.
# Never pushed to a registry.
#
# Usage (--platform=linux/amd64 is pinned in the FROM below; do not omit it):
#   docker build --target harvest -o type=local,dest=./glibc217-sysroot-out \
#       -f tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile \
#       tracer/build/_build/docker
#   tar -czf glibc217-sysroot-x86_64.tar.gz -C ./glibc217-sysroot-out .
#   sha512sum glibc217-sysroot-x86_64.tar.gz
#   # upload tarball + hash to apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/
#
# Without BuildKit -o/--output:
#   docker build --target harvest -t glibc217-harvest:local -f <this file> <context>
#   docker create --name tmp glibc217-harvest:local
#   docker cp tmp:/sysroot ./glibc217-sysroot-out && docker rm tmp

FROM --platform=linux/amd64 centos:7 AS base

# replace the centos repository with vault.centos.org because they shut down the original
RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # glibc-devel provides linker scripts + *_nonshared.a that the plain glibc package doesn't ship
    && yum install -y glibc glibc-devel glibc-headers \
    # centos-release-scl adds a new repo file pointing at dead mirrors, so repeat the fixup
    && yum install -y centos-release-scl \
    && sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # devtoolset-11 provides libstdc++.a/libgcc.a and C++ headers built against glibc 2.17;
    # a modern host's copies reference symbols newer than 2.17
    && yum install -y devtoolset-11-gcc-c++

# Stage only the specific glibc files needed at link time (not the whole /lib64 or
# /usr/lib64 trees). C headers are intentionally not harvested.
RUN set -eux; \
    mkdir -p /harvest/lib64 /harvest/usr/lib64; \
    for f in ld-linux-x86-64.so.2 ld-2.17.so \
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
    # devtoolset-11 libstdc++/libgcc static archives, linker stubs, and C++ headers
    DT11_LIB=/opt/rh/devtoolset-11/root/usr/lib/gcc/x86_64-redhat-linux/11; \
    DT11_INC=/opt/rh/devtoolset-11/root/usr/include/c++/11; \
    mkdir -p /harvest/devtoolset11/lib /harvest/devtoolset11/include; \
    cp -a $DT11_LIB/libstdc++.a $DT11_LIB/libstdc++.so $DT11_LIB/libgcc.a $DT11_LIB/libgcc_s.so $DT11_LIB/libgcc_eh.a /harvest/devtoolset11/lib/; \
    cp -a $DT11_INC /harvest/devtoolset11/include/11

FROM scratch AS harvest
# Export a flat layout so `docker build -o type=local,dest=<dir>` produces the structure
# ubuntu.dockerfile's extraction step expects.
COPY --from=base /harvest/ /
