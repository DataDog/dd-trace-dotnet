add_library(coreclr OBJECT
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/prebuilt/idl/corprof_i.cpp
)

target_include_directories(coreclr PUBLIC
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/inc/rt
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/prebuilt/inc
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/pal/inc
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/dotnet-runtime/coreclr/inc
)

target_compile_options(coreclr PUBLIC
    -std=c++20
    -DPAL_STDCPP_COMPAT
    -DPLATFORM_UNIX
    -DUNICODE
    -fms-extensions
    -DHOST_64BIT
    -Wno-pragmas
    -g
)
