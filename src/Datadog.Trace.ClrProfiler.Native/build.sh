#!/bin/bash

set -euxo pipefail

CORECLR_OS=Linux
CORECLR_ARCH=x64
CORECLR_CONFIGURATION=Debug

CXX_FLAGS="${CXX_FLAGS:-} \
    -Wno-invalid-noreturn \
    -fPIC \
    -fms-extensions \
    -DBIT64 \
    -DPAL_STDCPP_COMPAT \
    -DPLATFORM_UNIX \
    -std=c++11 \
    "

LD_FLAGS="${LD_FLAGS:-} \
    -I /opt/coreclr/src/pal/inc/rt \
    -I /opt/coreclr/src/pal/prebuilt/inc \
    -I /opt/coreclr/src/pal/inc \
    -I /opt/coreclr/src/inc \
    -I /opt/coreclr/bin/Product/$CORECLR_OS.$CORECLR_ARCH.$CORECLR_CONFIGURATION/inc \
    -I /opt/spdlog/include \
    "

mkdir -p obj/$CORECLR_CONFIGURATION/$CORECLR_ARCH
clang++-3.9 \
    -shared \
    -o obj/$CORECLR_CONFIGURATION/$CORECLR_ARCH/Datadog.Trace.ClrProfiler.Native.so \
    $CXX_FLAGS \
    $LD_FLAGS \
    class_factory.cpp \
    dllmain.cpp 
