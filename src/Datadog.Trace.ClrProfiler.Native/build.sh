#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
CORECLR_PATH=/opt/coreclr
BUILD_OS=Linux
BUILD_ARCH=x64

for BUILD_TYPE in Debug Release ; do

    BINDIR="$DIR/bin/$BUILD_TYPE/$BUILD_OS-$BUILD_ARCH/"

    printf '  CORECLR_PATH : %s\n' "$CORECLR_PATH"
    printf '  BUILD_OS      : %s\n' "$BUILD_OS"
    printf '  BUILD_ARCH    : %s\n' "$BUILD_ARCH"
    printf '  BUILD_TYPE    : %s\n' "$BUILD_TYPE"

    printf '  Building %s ... ' "$BINDIR"

    CXX_FLAGS="$CXX_FLAGS \
        -Wno-invalid-noreturn \
        -Wno-pragma-pack \
        -fPIC \
        -fms-extensions \
        -DBIT64 \
        -DPAL_STDCPP_COMPAT \
        -DPLATFORM_UNIX \
        -DUNICODE \
        -std=c++17 \
        -stdlib=libc++ \
    "
    LD_FLAGS="$LD_FLAGS \
        -I $CORECLR_PATH/src/pal/inc/rt \
        -I $CORECLR_PATH/src/pal/prebuilt/inc \
        -I $CORECLR_PATH/src/pal/inc \
        -I $CORECLR_PATH/src/inc \
        -I $CORECLR_PATH/bin/Product/$BUILD_OS.$BUILD_ARCH.$BUILD_TYPE/inc \
        -I ../../../vcpkg/packages/nlohmann-json_x64-osx/include \
        -I ../../../vcpkg/packages/fmt_x64-osx/include \
        -I ../../../vcpkg/packages/spdlog_x64-osx/include \
    "

    mkdir -p "$BINDIR"
    clang++-7 -shared -o "$BINDIR/Datadog.Trace.ClrProfiler.Native.so" $CXX_FLAGS $LD_FLAGS \
        metadata_builder.cpp \
        util.cpp

done

printf 'Done.\n'
