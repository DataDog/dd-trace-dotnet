The files here were copied from https://github.com/dotnet/runtime/tree/v7.0.0/src/coreclr/src.

This is to allow using the runtime's Platform Adaptation Layer.

Commented #define statements because there is naming conflicts when compiling with the stdlibc++ 8 (+ C++17)
in `dotnet-runtime-coreclr\pal\inc\rt\sal.h`
l.2612    // commented because it conflicts with stdlibc++ 8
l.2613    //#define __valid

and

l.2622    // commented because it conflicts with stdlibc++ 8
l.2623    //#define __pre

in `corprof_i.cpp`, add before the #include:
bool g_arm64_atomics_present = false;

in `corhlpr.cpp`, use in debug only
#ifdef _DEBUG
    assert(&origBuff[size] == outBuff);
#endif

in `pal.h`, add the definition of g_arm64_atomics_present after Processor-specific glue
#if defined(HOST_ARM64)
// Flag to check if atomics feature is available on
// the machine
extern bool g_arm64_atomics_present;
#endif

in `shared/src/native-lib/coreclr/src/pal/inc/rt/specstrings.h` (l.317) commented `#define __bound` to fix linux compilation
