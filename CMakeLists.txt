cmake_minimum_required (VERSION 3.8..3.19)
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

add_subdirectory(tracer)

# profiler cannot be built on MacOS (yet)
if (NOT ISMACOS)
    add_subdirectory(profiler)
endif()

add_subdirectory(shared)
