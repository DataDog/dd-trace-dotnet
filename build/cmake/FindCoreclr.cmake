add_library(coreclr OBJECT
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/prebuilt/idl/corprof_i.cpp
)

target_include_directories(coreclr PUBLIC
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/inc/rt
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/prebuilt/inc
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/inc
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/inc
    # Parent of minipal/, not coreclr/ itself: pal.h includes <minipal/utils.h> and
    # pal_mstypes.h includes <minipal/guid.h>. Appended, so it can't shadow anything above.
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime
)

target_compile_options(coreclr PUBLIC
    -std=c++20
    -DPLATFORM_UNIX
    -DUNICODE
    -fms-extensions
    -DHOST_64BIT
    -Wno-pragmas
    -g
    # Defines exactly one of HOST_AMD64/HOST_ARM64/HOST_X86, which the vendored pal headers
    # require the build to provide from .NET 11 onwards. Must be a forced include, not a
    # -D flag: the macOS universal build compiles both arm64 and x86_64 slices in a single
    # clang invocation, and only the compiler's own built-in arch macros (which this header
    # is written in terms of) are still correct per slice in that case. See the header for
    # the full explanation.
    -include ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/host_arch.h
)
