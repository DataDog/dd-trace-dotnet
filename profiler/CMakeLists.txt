# Out of source build directory
set(OUTPUT_BUILD_DIR ${CMAKE_CURRENT_SOURCE_DIR}/_build)

if (DEFINED RUN_ANALYSIS AND NOT RUN_ANALYSIS EQUAL 0)
    set(CMAKE_EXPORT_COMPILE_COMMANDS 1)
endif()

if (DEFINED RUN_ANALYSIS AND RUN_ANALYSIS EQUAL 1)
    find_program(FOUND_CLANGTIDY clang-tidy)
    if (NOT FOUND_CLANGTIDY)
        message(FATAL_ERROR "clang-tidy is required to build and analyze the project")
    else()
        message(STATUS "clang-tidy was found")
    endif()
endif()

# ******************************************************
# Output folders
# ******************************************************

# Deployment directory

if (ISAMD64)
    set(ARCH_POSTFIX "x64")
elseif (ISARM64)
    set(ARCH_POSTFIX "arm64")
elseif (ISARM)
    set(ARCH_POSTFIX "arm")
else()
    set(ARCH_POSTFIX "x86")
endif()

set(ARCH_BASENAME "linux")
if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(IS_ALPINE TRUE)
    set(ARCH_BASENAME "${ARCH_BASENAME}-musl")
endif()

set(DEPLOY_DIR ${OUTPUT_BUILD_DIR}/DDProf-Deploy/${ARCH_BASENAME}-${ARCH_POSTFIX})

# ******************************************************
# Common compile options (INTERFACE library)
# ******************************************************
# Targets link against this to inherit shared compiler/linker settings.

add_library(profiler_compile_options INTERFACE)

target_compile_options(profiler_compile_options INTERFACE
    -fPIC -fms-extensions
    -Wno-invalid-noreturn -Wno-macro-redefined
)

target_compile_definitions(profiler_compile_options INTERFACE
    PAL_STDCPP_COMPAT PLATFORM_UNIX UNICODE
)

if (IS_ALPINE)
    target_compile_definitions(profiler_compile_options INTERFACE DD_ALPINE)
endif()

if (ISLINUX)
    target_compile_options(profiler_compile_options INTERFACE -stdlib=libstdc++ -Wno-pragmas)
    target_compile_definitions(profiler_compile_options INTERFACE LINUX)
endif()

if (BIT64)
    target_compile_definitions(profiler_compile_options INTERFACE BIT64 HOST_64BIT)
endif()

if (ISAMD64)
    target_compile_definitions(profiler_compile_options INTERFACE AMD64)
elseif (ISX86)
    target_compile_definitions(profiler_compile_options INTERFACE BX86)
elseif (ISARM64)
    target_compile_definitions(profiler_compile_options INTERFACE ARM64)
elseif (ISARM)
    target_compile_definitions(profiler_compile_options INTERFACE ARM)
endif()

# Sanitizers (compile + link)
option(RUN_ASAN "Build with Clang Address Sanitizer" OFF)
option(RUN_UBSAN "Build with Clang Undefined-Behavior Sanitizer" OFF)
option(RUN_TSAN "Build with Clang Thread Sanitizer" OFF)

message(STATUS "Run Clang Address Sanitizer: " ${RUN_ASAN})
message(STATUS "Run Clang Undefined-Behavior Sanitizer: " ${RUN_UBSAN})
message(STATUS "Run Clang Thread Sanitizer: " ${RUN_TSAN})

if (RUN_ASAN)
    target_compile_options(profiler_compile_options INTERFACE -g -fsanitize=address -fno-omit-frame-pointer)
    target_compile_definitions(profiler_compile_options INTERFACE DD_SANITIZERS)
    target_link_options(profiler_compile_options INTERFACE -fsanitize=address)
endif()

if (RUN_UBSAN)
    target_compile_options(profiler_compile_options INTERFACE -fsanitize=undefined -g -fno-omit-frame-pointer -fno-sanitize-recover=all)
    target_compile_definitions(profiler_compile_options INTERFACE DD_SANITIZERS)
    target_link_options(profiler_compile_options INTERFACE -fsanitize=undefined)
endif()

if (RUN_TSAN)
    target_compile_options(profiler_compile_options INTERFACE -fsanitize=thread -g -fno-omit-frame-pointer)
    target_compile_definitions(profiler_compile_options INTERFACE DD_SANITIZERS)
    target_link_options(profiler_compile_options INTERFACE -fsanitize=thread)
endif()

# Set output folders
set(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
set(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
file(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})
file(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})

add_subdirectory(src/ProfilerEngine/Datadog.Profiler.Native.Linux)
add_subdirectory(src/ProfilerEngine/Datadog.Linux.ApiWrapper)
enable_testing()
add_subdirectory(test)

add_custom_target(profiler)
add_dependencies(profiler Datadog.Profiler.Native)

add_custom_target(all-profiler)
add_dependencies(all-profiler profiler profiler-native-tests)

add_custom_target(wrapper)
add_dependencies(wrapper Datadog.Linux.ApiWrapper.x64)

add_custom_target(all-wrapper)
add_dependencies(all-wrapper wrapper wrapper-native-tests)
