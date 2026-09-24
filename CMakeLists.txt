cmake_minimum_required (VERSION 3.13.4)
cmake_policy(SET CMP0015 NEW)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

# macOS uses 3.30 which deprecates FetchContent_Populate in favor of FetchContent_MakeAvailable,
# but we're using 3.13.4 on Linux, which doesn't have FetchContent_MakeAvailable

if(POLICY CMP0169)
    cmake_policy(SET CMP0169 OLD)
endif()

# ******************************************************
# Project definition
# ******************************************************

project("Datadog.APM.Native" VERSION 2.18.0)

# ******************************************************
# Environment detection
# ******************************************************

option(UNIVERSAL "Build as Universal binary" ON)
message(STATUS "Building Native Loader and Wrapper as Universal binary: " ${UNIVERSAL})

# UNIVERSAL (musl) and USE_GLIBC217_SYSROOT are mutually exclusive.
if (UNIVERSAL AND USE_GLIBC217_SYSROOT)
    message(FATAL_ERROR "UNIVERSAL and USE_GLIBC217_SYSROOT are mutually exclusive: the "
                        "Universal build targets musl, the glibc217 sysroot build targets "
                        "glibc 2.17. Pick one - drop -DUNIVERSAL=ON to build the native "
                        "loader/wrapper against the glibc 2.17 sysroot, or drop "
                        "-DCMAKE_TOOLCHAIN_FILE=build/cmake/Glibc217.cmake.x86_64 to build "
                        "them as Universal/musl binaries.")
endif()

# Centralized C++ stdlib flag. Empty under USE_GLIBC217_SYSROOT (Glibc217.cmake.x86_64
# already controls header search and linking; -stdlib=libstdc++ on top triggers warnings).
if (USE_GLIBC217_SYSROOT)
    set(DD_CXX_STDLIB_COMPILE_OPTIONS "")
else()
    set(DD_CXX_STDLIB_COMPILE_OPTIONS -stdlib=libstdc++)
endif()

if (CMAKE_TOOLCHAIN_FILE)
    message(STATUS "Using toolchain file ${CMAKE_TOOLCHAIN_FILE}")
endif()

# Detect stale build directory: CMake only sources a toolchain file on first configure.
# Re-running with -DCMAKE_TOOLCHAIN_FILE on an existing cache silently ignores it.
if (CMAKE_TOOLCHAIN_FILE MATCHES "Glibc217" AND NOT USE_GLIBC217_SYSROOT)
    message(FATAL_ERROR
        "CMAKE_TOOLCHAIN_FILE is set to '${CMAKE_TOOLCHAIN_FILE}' but that file was never "
        "sourced, so NONE of its glibc-2.17 compile/link flags are in effect. This happens "
        "when a toolchain file is added to a build directory that was already configured "
        "without one - CMake accepts the variable but silently ignores the file. Left "
        "unchecked this produces a binary that links against the build host's own glibc and "
        "still builds successfully, so delete the build directory (${CMAKE_BINARY_DIR}) and "
        "configure it again from scratch.")
endif()

SET(OSX_ARCH ${CMAKE_OSX_ARCHITECTURES})

# Detect architecture
if (OSX_ARCH STREQUAL x86_64)
    message(STATUS "Architecture is x64/AMD64 configured by CMAKE_OSX_ARCHITECTURES")
    SET(ISAMD64 true)
elseif (OSX_ARCH STREQUAL arm64)
    message(STATUS "Architecture is ARM64 configured by CMAKE_OSX_ARCHITECTURES")
    SET(ISARM64 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL x86_64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL amd64)
    message(STATUS "Architecture is x64/AMD64")
    SET(ISAMD64 true)
    SET(OSX_ARCH "x86_64")
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL x86 OR CMAKE_SYSTEM_PROCESSOR STREQUAL i686)
    message(STATUS "Architecture is x86")
    SET(ISX86 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    message(STATUS "Architecture is ARM64")
    SET(ISARM64 true)
    SET(OSX_ARCH "arm64")
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL armv7l OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm)
    message(STATUS "Architecture is ARM")
    SET(ISARM true)
endif()

# Detect operating system
if (CMAKE_SYSTEM_NAME MATCHES "Windows")
    message(FATAL_ERROR "Windows builds are not supported using CMAKE. Please use Visual Studio")
    SET(ISWINDOWS true)
elseif (CMAKE_SYSTEM_NAME MATCHES "Linux")
    message(STATUS "Preparing Linux build")
    SET(ISLINUX true)
elseif (CMAKE_SYSTEM_NAME MATCHES "Darwin")
    message(STATUS "Preparing macOS build")
    SET(ISMACOS true)
endif()

# Detect bitness of the build
if (CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(STATUS "Setting compilation for 64bits processor")
    SET(BIT64 true)
endif()

SET(DOTNET_TRACER_REPO_ROOT_PATH ${CMAKE_CURRENT_SOURCE_DIR})

LIST(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_LIST_DIR}/build/cmake")

find_package(Coreclr REQUIRED)
message(STATUS "Coreclr files ")

find_package(Re2 REQUIRED)
message(STATUS "Re2 library " ${RE2_VERSION})

find_package(Spdlog REQUIRED)
message(STATUS "Spdlog headers")

find_package(ManagedLoader REQUIRED)
message(STATUS "Tracer Managed Loader")

find_package(Libunwind REQUIRED)
message(STATUS "Libunwind library " ${LIBUNWIND_VERSION})

find_package(Libdatadog REQUIRED)
message(STATUS "Libdatadog library " ${LIBDATADOG_VERSION})

find_package(GoogleTest REQUIRED)
message(STATUS "GoogleTest library")

find_package(PPDB REQUIRED)
message(STATUS "PPDB library")

# Defined once here (not per-subdirectory) to avoid duplicate target names in a single configure.
if (UNIVERSAL)
    find_package(GlibcCompat REQUIRED)
    message(STATUS "Glibc Compat objects")
endif()

# Separate from glibc-compat above (see FindGlibc217Compat.cmake). Same once-here pattern.
if (USE_GLIBC217_SYSROOT)
    find_package(Glibc217Compat REQUIRED)
    message(STATUS "Glibc217 Compat objects")
endif()

add_subdirectory(tracer)

# profiler cannot be built on MacOS (yet)
if (NOT ISMACOS)
    add_subdirectory(profiler)
endif()

add_subdirectory(shared)
