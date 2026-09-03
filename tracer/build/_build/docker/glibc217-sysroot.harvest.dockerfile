# syntax=docker/dockerfile:1.6
#
# One-time (or once-every-great-while - glibc 2.17 doesn't change) extraction tool. This
# image is NEVER pushed to any registry and nothing ever does `FROM` on it - its only job is
# to let a human pull /sysroot/x86_64-glibc217 out onto disk as a tarball. See "Harvest the
# sysroot" in docs/development/rfc-linux-x64-build-host.md.
#
# --platform=linux/amd64 is pinned explicitly below - do not rely on `docker build
# --platform=` to get this right, the daemon's default platform depends on the host running
# the build (e.g. this produces silently-wrong aarch64 glibc files if built on an arm64 host
# without the pin).
#
# Usage:
#   docker build --target harvest -o type=local,dest=./glibc217-sysroot-out \
#       -f tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile \
#       tracer/build/_build/docker
#   tar -czf glibc217-sysroot-x86_64.tar.gz -C ./glibc217-sysroot-out .
#   sha512sum glibc217-sysroot-x86_64.tar.gz
#   # upload both the tarball and the printed hash to
#   # apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/, then discard
#   # ./glibc217-sysroot-out and this image.
#
# If BuildKit's -o/--output export isn't available, fall back to:
#   docker build --target harvest -t glibc217-harvest:local -f <this file> <context>
#   docker create --name tmp glibc217-harvest:local
#   docker cp tmp:/sysroot ./glibc217-sysroot-out && docker rm tmp

FROM --platform=linux/amd64 centos:7 AS base

# replace the centos repository with vault.centos.org because they shut down the original
RUN sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # glibc-devel pulls in glibc-headers too, and (critically) the /usr/lib64/*.so linker
    # scripts + *_nonshared.a archives that the plain `glibc` package doesn't ship - without
    # these, -lc simply fails to resolve at link time. No compiler toolchain is installed
    # here for COMPILING (the build host compiles with its own modern headers - see
    # Glibc217.cmake.x86_64's comment for why that's safe for C); this image only needs to
    # supply old glibc's own files for that part.
    && yum install -y glibc glibc-devel glibc-headers \
    # centos-release-scl adds ITS OWN new repo file (centos-sclo-rh) pointing at the same
    # dead mirror.centos.org - the fixup above ran before that file existed, so it has to be
    # repeated here or the subsequent devtoolset-11 install fails with "Cannot find a valid
    # baseurl for repo: centos-sclo-rh/x86_64" (verified).
    && yum install -y centos-release-scl \
    && sed -i s/mirror.centos.org/vault.centos.org/g /etc/yum.repos.d/*.repo \
    && sed -i s/^#.*baseurl=http/baseurl=http/g /etc/yum.repos.d/*.repo \
    && sed -i s/^mirrorlist=http/#mirrorlist=http/g /etc/yum.repos.d/*.repo \
    # devtoolset-11 is needed for a DIFFERENT reason: -static-libstdc++/-static-libgcc pull
    # in libstdc++.a/libgcc.a from wherever clang auto-detects a GCC install, and a build
    # host's OWN (modern) GCC's libstdc++.a internally calls glibc functions newer than 2.17
    # (__libc_single_threaded, __cxa_thread_atexit_impl, pthread_cond_clockwait - verified in
    # CI: "undefined symbol" for all three, referenced from libstdc++.a and from inline
    # header code compiled against modern C++ headers). devtoolset-11 is Red Hat's own GCC
    # 11 rebuilt specifically to still target RHEL7's glibc floor - same GCC major version as
    # a typical modern host's system GCC, but built to not assume anything newer than glibc
    # 2.17 is present. It's also the newest devtoolset CentOS 7 ever shipped (there is no
    # devtoolset-12) and is already a dependency of this pipeline elsewhere
    # (centos7.build.dockerfile uses it to bootstrap clang-16). Both its libstdc++.a/libgcc.a
    # AND its C++ headers are needed - the glibc-version-specific implementation of
    # functions like std::condition_variable::wait_until is header-inlined, so using a
    # modern host's C++ headers reintroduces the same problem even with old libraries linked.
    && yum install -y devtoolset-11-gcc-c++

# Stage only the specific glibc files actually needed at link time - deliberately NOT the
# whole /lib64 or /usr/lib64 trees, which also contain unrelated packages baked into the
# centos:7 base image (libcrypto, libcryptsetup, ...), some with permission bits that choke
# BuildKit's local-directory export (e.g. pm-utils/module.d). /usr/include (glibc's own C
# headers) is intentionally not harvested: Glibc217.cmake.x86_64 never points header search
# at this sysroot for C, so they'd be dead weight. File list verified against a real
# x86_64 centos:7 + glibc-devel container.
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
    # devtoolset-11's libstdc++/libgcc (both the static archives -static-libstdc++/
    # -static-libgcc need, and the small .so linker-script stubs) plus its C++ headers
    # (main + the arch-specific subdir, both required - the latter has bits/c++config.h).
    DT11_LIB=/opt/rh/devtoolset-11/root/usr/lib/gcc/x86_64-redhat-linux/11; \
    DT11_INC=/opt/rh/devtoolset-11/root/usr/include/c++/11; \
    mkdir -p /harvest/devtoolset11/lib /harvest/devtoolset11/include; \
    cp -a $DT11_LIB/libstdc++.a $DT11_LIB/libstdc++.so $DT11_LIB/libgcc.a $DT11_LIB/libgcc_s.so $DT11_LIB/libgcc_eh.a /harvest/devtoolset11/lib/; \
    cp -a $DT11_INC /harvest/devtoolset11/include/11

FROM scratch AS harvest
# Copy to this stage's own root (NOT /sysroot/x86_64-glibc217/) so that `docker build -o
# type=local,dest=<dir>` exports a flat <dir>/lib64/, <dir>/usr/lib64/, <dir>/devtoolset11/ -
# matching what the tar command below and ubuntu.dockerfile's extraction step both expect.
# Baking the /sysroot/x86_64-glibc217 prefix into this image would double-nest it once
# extracted there.
COPY --from=base /harvest/ /
