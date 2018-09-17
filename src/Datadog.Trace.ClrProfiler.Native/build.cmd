@echo off

SET CORECLR_PATH=../../lib/coreclr
SET Output=Datadog.Trace.ClrProfiler.Native.dll

SET CXX_FLAGS=--no-undefined -Wno-invalid-noreturn -fms-extensions -DPAL_STDCPP_COMPAT -DPLATFORM_WINDOWS -DBIT64=1 -DAMD64 -D_AMD64_ -D_WIN64 -std=c++14
SET INCLUDES=-I %CORECLR_PATH%/src/pal/inc/rt -I %CORECLR_PATH%/src/pal/prebuilt/inc -I %CORECLR_PATH%/src/pal/inc -I %CORECLR_PATH%/src/inc

clang++ -shared -o %Output% %CXX_FLAGS% %INCLUDES% *.cpp
