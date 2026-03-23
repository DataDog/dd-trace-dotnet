cmake_minimum_required (VERSION 3.13.4)
cmake_policy(SET CMP0015 NEW)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
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

if (CMAKE_TOOLCHAIN_FILE)
    message(STATUS "Using toolchain file ${CMAKE_TOOLCHAIN_FILE}")
endif()

set(OSX_ARCH ${CMAKE_OSX_ARCHITECTURES})

# Detect architecture
if (OSX_ARCH STREQUAL x86_64)
    message(STATUS "Architecture is x64/AMD64 configured by CMAKE_OSX_ARCHITECTURES")
    set(ISAMD64 true)
elseif (OSX_ARCH STREQUAL arm64)
    message(STATUS "Architecture is ARM64 configured by CMAKE_OSX_ARCHITECTURES")
    set(ISARM64 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL x86_64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL amd64)
    message(STATUS "Architecture is x64/AMD64")
    set(ISAMD64 true)
    set(OSX_ARCH "x86_64")
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL x86 OR CMAKE_SYSTEM_PROCESSOR STREQUAL i686)
    message(STATUS "Architecture is x86")
    set(ISX86 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    message(STATUS "Architecture is ARM64")
    set(ISARM64 true)
    set(OSX_ARCH "arm64")
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL armv7l OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm)
    message(STATUS "Architecture is ARM")
    set(ISARM true)
endif()

# Detect operating system
if (CMAKE_SYSTEM_NAME MATCHES "Windows")
    message(FATAL_ERROR "Windows builds are not supported using CMAKE. Please use Visual Studio")
elseif (CMAKE_SYSTEM_NAME MATCHES "Linux")
    message(STATUS "Preparing Linux build")
    set(ISLINUX true)
elseif (CMAKE_SYSTEM_NAME MATCHES "Darwin")
    message(STATUS "Preparing macOS build")
    set(ISMACOS true)
endif()

# Detect bitness of the build
if (CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(STATUS "Setting compilation for 64bits processor")
    set(BIT64 true)
endif()

# ******************************************************
# Detect prerequisites
# ******************************************************

find_program(FOUND_GIT "git")
if (NOT FOUND_GIT)
    message(FATAL_ERROR "GIT is required to build the project")
else()
    message(STATUS "GIT was found")
endif()

find_program(FOUND_GCC gcc)
if (NOT FOUND_GCC)
    message(FATAL_ERROR "GCC is required to build the project's dependencies")
else()
    message(STATUS "GCC was found")
endif()

find_program(FOUND_CLANG clang)
if (NOT FOUND_CLANG)
    message(FATAL_ERROR "CLANG is required to build the project")
else()
    message(STATUS "CLANG was found")
endif()

find_program(FOUND_CLANGPP clang++)
if (NOT FOUND_CLANGPP)
    message(FATAL_ERROR "CLANG++ is required to build the project")
else()
    message(STATUS "CLANG++ was found")
endif()

# ******************************************************
# Suppress Warning on MacOS
# ******************************************************

# Only necessary with cmake 3.19.x on macos
# See https://stackoverflow.com/questions/4929255/building-static-libraries-on-mac-using-cmake-and-gcc#answer-4953904

if (ISMACOS)
    set(CMAKE_C_ARCHIVE_CREATE   "<CMAKE_AR> Scr <TARGET> <LINK_FLAGS> <OBJECTS>")
    set(CMAKE_CXX_ARCHIVE_CREATE "<CMAKE_AR> Scr <TARGET> <LINK_FLAGS> <OBJECTS>")
    set(CMAKE_C_ARCHIVE_FINISH   "<CMAKE_RANLIB> -no_warning_for_no_symbols -c <TARGET>")
    set(CMAKE_CXX_ARCHIVE_FINISH "<CMAKE_RANLIB> -no_warning_for_no_symbols -c <TARGET>")
endif()

add_compile_definitions(_GLIBCXX_USE_CXX11_ABI=0)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(DOTNET_TRACER_REPO_ROOT_PATH ${CMAKE_CURRENT_SOURCE_DIR})

list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_LIST_DIR}/build/cmake")

include(Hardening)

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

add_subdirectory(tracer)

# profiler cannot be built on MacOS (yet)
if (NOT ISMACOS)
    add_subdirectory(profiler)
endif()

add_subdirectory(shared)
