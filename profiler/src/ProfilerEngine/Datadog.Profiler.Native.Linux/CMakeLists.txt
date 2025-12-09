# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Profiler.Native.Linux" VERSION 3.32.1)

option(RUN_ASAN "Build with Clang Undefined-Behavior Sanitizer" OFF)
option(RUN_UBSAN "Build with Clang Undefined-Behavior Sanitizer" OFF)
option(RUN_TSAN "Build with Clang Thread Sanitizer" OFF)

message(STATUS "Run Clang Address Sanitizer: " ${RUN_ASAN})
message(STATUS "Run Clang Undefined-Behavior Sanitizer: " ${RUN_UBSAN})
message(STATUS "Run Clang Thread Sanitizer: " ${RUN_TSAN})

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-std=c++20 -fPIC -fms-extensions -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

if (IS_ALPINE)
    add_compile_options(-DDD_ALPINE)
endif()

if (RUN_ASAN)
    add_compile_options(-g -fsanitize=address -fno-omit-frame-pointer -DDD_SANITIZERS)
endif()

if (RUN_UBSAN)
    add_compile_options(-fsanitize=undefined -g -fno-omit-frame-pointer -fno-sanitize-recover=all -DDD_SANITIZERS)
endif()

if (RUN_TSAN)
    add_compile_options(-fsanitize=thread -g -fno-omit-frame-pointer -DDD_SANITIZERS)
endif()

if(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
endif()

if (BIT64)
    add_compile_options(-DBIT64)
    add_compile_options(-DHOST_64BIT)
endif()

if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DBX86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()

if (ISLINUX)
    # ------------------------------------------------------
    # Hardening: make sure no target in this project ever
    # requests an executable stack.  Without this, glibc ≥2.41
    # (Debian 13 "trixie", Fedora 40, etc.) rejects the shared
    # library with:
    #   "cannot enable executable stack as shared object requires"
    # ------------------------------------------------------
    # 1. Tell the assembler to emit a .note.GNU-stack note that
    #    marks the object as **non‑exec‑stack**.
    add_compile_options("$<$<COMPILE_LANGUAGE:ASM>:-Wa,--noexecstack>")
    # 2. Instruct the linker to *clear* any stray exec‑stack flag
    #    that might still be present when it produces the final ELF.
    add_link_options(-Wl,-z,noexecstack)
endif()

# ******************************************************
# Environment detection
# ******************************************************

SET(PROFILER_BASENAME Datadog.Profiler.Native)
SET(PROFILER_STATIC_LIB_NAME ${PROFILER_BASENAME}.static)
SET(PROFILER_SHARED_LIB_NAME ${PROFILER_BASENAME})

SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR})

# ******************************************************
# Dependencies
# ******************************************************

FILE(GLOB LINUX_PROFILER_SRC CONFIGURE_DEPENDS "*.cpp")

FILE(GLOB COMMON_PROFILER_SRC LIST_DIRECTORIES false "../Datadog.Profiler.Native/*.cpp")
FILE(GLOB EXCLUDE_DLLMAIN "../Datadog.Profiler.Native/DllMain.cpp")

list(REMOVE_ITEM COMMON_PROFILER_SRC "${EXCLUDE_DLLMAIN}")

# ******************************************************
# Define static target
# ******************************************************
add_library(${PROFILER_STATIC_LIB_NAME} STATIC
    ${LINUX_PROFILER_SRC}
    ${COMMON_PROFILER_SRC}
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
)

set_target_properties(${PROFILER_STATIC_LIB_NAME} PROPERTIES PREFIX "")

# Define directories includes
target_include_directories(${PROFILER_STATIC_LIB_NAME}
    PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/../Datadog.Profiler.Native.Linux
    PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/../Datadog.Profiler.Native
    PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}
)

# Define linker libraries

if (RUN_UBSAN)
    target_link_libraries(${PROFILER_STATIC_LIB_NAME} -fsanitize=undefined)
endif()

if (RUN_ASAN)
    target_link_libraries(${PROFILER_STATIC_LIB_NAME} -fsanitize=address)
endif()

if (NOT RUN_ASAN AND NOT RUN_UBSAN AND NOT RUN_TSAN)
    target_link_libraries(${PROFILER_STATIC_LIB_NAME} -Wl,--no-undefined)
endif()

target_compile_definitions(${PROFILER_STATIC_LIB_NAME} PUBLIC "-D_GLIBCXX_USE_CXX11_ABI=0")

target_link_libraries(${PROFILER_STATIC_LIB_NAME}
    libunwind-lib
    libdatadog-lib
    coreclr
    PPDB
    spdlog-headers
    -static-libgcc
    -static-libstdc++
    -lstdc++fs
    -pthread
    -ldl
    -Wl,--build-id=sha1
)

add_dependencies(${PROFILER_STATIC_LIB_NAME} libdatadog-lib libunwind-lib coreclr spdlog-headers PPDB)

# ******************************************************
# Define shared target
# ******************************************************
set(dd_profiling_linker_script "${CMAKE_CURRENT_SOURCE_DIR}/datadog_profiling.version")

add_library(${PROFILER_SHARED_LIB_NAME} SHARED
    ../Datadog.Profiler.Native/DllMain.cpp
)

set_target_properties(${PROFILER_SHARED_LIB_NAME} PROPERTIES PREFIX "")
set_target_properties(${PROFILER_SHARED_LIB_NAME} PROPERTIES LINK_DEPENDS "${dd_profiling_linker_script}")
target_link_options(${PROFILER_SHARED_LIB_NAME} PRIVATE
                    "LINKER:--version-script=${dd_profiling_linker_script}")

# Define linker libraries
target_link_libraries(${PROFILER_SHARED_LIB_NAME}
    -Wl,--whole-archive $<TARGET_FILE:${PROFILER_STATIC_LIB_NAME}> -Wl,--no-whole-archive
    -Wl,-rpath,'$ORIGIN'
    ${PROFILER_STATIC_LIB_NAME})
