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

# Mutually exclusive by construction: UNIVERSAL builds the native loader and wrapper against
# musl (hence the glibc-compat shim below), USE_GLIBC217_SYSROOT builds against a frozen
# glibc 2.17 sysroot. There is no "both" - a single loader/wrapper binary can only have one
# libc. Glibc217.cmake.x86_64 already seeds UNIVERSAL=OFF in the cache so the ordinary
# configure picks the right one on its own; reaching here with both ON means someone passed
# -DUNIVERSAL=ON explicitly alongside the glibc217 toolchain. Fail loudly at configure time,
# because the alternative is a confusing compile error deep in the musl shim
# ("conflicting types for 'strerror_r'") that doesn't hint at the real cause.
if (UNIVERSAL AND USE_GLIBC217_SYSROOT)
    message(FATAL_ERROR "UNIVERSAL and USE_GLIBC217_SYSROOT are mutually exclusive: the "
                        "Universal build targets musl, the glibc217 sysroot build targets "
                        "glibc 2.17. Pick one - drop -DUNIVERSAL=ON to build the native "
                        "loader/wrapper against the glibc 2.17 sysroot, or drop "
                        "-DCMAKE_TOOLCHAIN_FILE=build/cmake/Glibc217.cmake.x86_64 to build "
                        "them as Universal/musl binaries.")
endif()

# The C++ standard-library selection flag used by every native target's ISLINUX branch.
# Empty under USE_GLIBC217_SYSROOT: Glibc217.cmake.x86_64 already sets -nostdinc++ plus
# -cxx-isystem <devtoolset-11 headers>, and links devtoolset-11's own libstdc++.a via
# -static-libstdc++ with -B/-L pointing at it, so header search AND the library are both
# already fully determined - -stdlib=libstdc++ on top is redundant, and clang warns
# "argument unused during compilation" for it on every single translation unit (71 in a full
# build, which drowns out real diagnostics). Centralized here rather than re-deriving the
# same condition at each of the seven ISLINUX sites that need the flag.
if (USE_GLIBC217_SYSROOT)
    set(DD_CXX_STDLIB_COMPILE_OPTIONS "")
else()
    set(DD_CXX_STDLIB_COMPILE_OPTIONS -stdlib=libstdc++)
endif()

if (CMAKE_TOOLCHAIN_FILE)
    message(STATUS "Using toolchain file ${CMAKE_TOOLCHAIN_FILE}")
endif()

# Catch a silent-wrong-artifact trap. CMake only SOURCES a toolchain file when it first
# configures a build directory (it is included from the generated CMakeFiles/*/CMakeSystem.cmake
# during project()); passing -DCMAKE_TOOLCHAIN_FILE= at a build dir that already has a cache
# stores the variable but never includes the file, with no warning of any kind. Every
# Compile*/Build* Nuke target shares one NativeBuildDirectory, so whichever one configures it
# first decides the toolchain for all of them.
#
# The consequence is worse than a build failure: with the toolchain silently skipped, none of
# the -nostdinc++/-cxx-isystem/--sysroot flags apply, so the native libs compile and link
# cleanly against the build host's own modern glibc and the build REPORTS SUCCESS - just with a
# GLIBC_2.36 floor instead of 2.17 (observed exactly this way: a full profiler build succeeded
# and `nm --with-symbol-versions` then showed pthread_create@GLIBC_2.34, dlopen@GLIBC_2.34,
# arc4random@GLIBC_2.36 ...). Only the separate ValidateNative*GlibcCompatibility step would
# notice, and only if it runs.
#
# Detect it precisely: CMAKE_TOOLCHAIN_FILE names the glibc217 file, yet the variable that file
# unconditionally sets is absent - which can only mean it was never actually sourced.
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

# Defined exactly once here, not per-subdirectory: glibc-compat (build/cmake/FindGlibcCompat.cmake)
# is needed by shared/ under UNIVERSAL. add_subdirectory(tracer) and add_subdirectory(profiler)
# below run in the SAME configure pass, so more than one subdirectory calling
# find_package(GlibcCompat REQUIRED) independently would fail with "add_library cannot create
# target glibc-compat because another target with the same name already exists" (this is
# exactly why USE_GLIBC217_SYSROOT was removed from this condition below - see that note).
if (UNIVERSAL)
    find_package(GlibcCompat REQUIRED)
    message(STATUS "Glibc Compat objects")
endif()

# Separate from the glibc-compat above (see FindGlibc217Compat.cmake for why) - needed by
# tracer/ and profiler/ when USE_GLIBC217_SYSROOT is set (Approach A of
# docs/development/rfc-linux-x64-build-host.md). Defined once here for the same
# add_subdirectory-runs-in-one-configure-pass reason as the glibc-compat block above.
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
