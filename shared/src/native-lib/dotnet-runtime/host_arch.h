#pragma once

// The vendored CoreCLR pal/inc headers (see README.md in this directory) used to derive
// HOST_AMD64/HOST_ARM64/HOST_X86 from the compiler's own architecture macros themselves.
// Starting with .NET 11 they no longer do - upstream now expects the *build* to define
// exactly one of these, e.g. inc/clrnt.h and pal/inc/pal.h branch on them directly. See
// shared/src/native-lib/dotnet-runtime/README.md for the upstream commit that dropped it.
//
// This can't be done with a single "-DHOST_ARM64" style compiler flag on macOS: the build
// produces a universal arm64+x86_64 binary with CMAKE_OSX_ARCHITECTURES set to both values
// at once, which compiles both architecture slices in a single clang invocation. A macro
// passed on the command line is textually identical for every slice of that invocation, so
// a flag computed once from CMAKE_SYSTEM_PROCESSOR would silently give the "wrong" slice the
// "wrong" host architecture. The compiler's own built-in architecture macros, by contrast,
// are still expanded correctly per slice (that's how a fat binary's two halves end up with
// different code in the first place), so this header derives HOST_* from those instead and
// is force-included ahead of anything that reaches the vendored pal headers.
//
// If a project already defines one of these itself (e.g. an older per-platform vcxproj
// setting), leave it alone rather than fight it.
#if defined(HOST_AMD64) || defined(HOST_ARM64) || defined(HOST_X86)
    // Already defined - nothing to do.
#elif defined(_M_X64) || defined(__x86_64__)
    #define HOST_AMD64
#elif defined(_M_ARM64) || defined(__aarch64__) || defined(__arm64__)
    #define HOST_ARM64
#elif defined(_M_IX86) || defined(__i386__)
    #define HOST_X86
#else
    #error "Could not determine HOST_AMD64/HOST_ARM64/HOST_X86 for this target - see shared/src/native-lib/dotnet-runtime/host_arch.h"
#endif
