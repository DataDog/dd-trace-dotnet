cmake_minimum_required(VERSION 3.13.4) # matches this repo's other native CMakeLists.txt pins
project(libdatadog_profiling_poc C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# find_package(PPDB REQUIRED) in the root CMakeLists.txt (called before this
# add_subdirectory) runs build/cmake/FindPPDB.cmake, which calls
# add_compile_options(-std=c++20 ...) in the ROOT scope - find_package()
# doesn't create a new directory scope, so that flag is inherited by every
# add_subdirectory() the root file calls afterward, this one included. Every
# other such subdirectory in this repo is C++, so it's harmless there; this
# is a pure C project, and clang rejects -std=c++20 outright when compiling a
# .c file. Reset the inherited property before defining any targets so
# nothing here (or curl/zstd, added below) sees it.
set_directory_properties(PROPERTIES COMPILE_OPTIONS "")

# This CMakeLists.txt is never invoked on Windows - the real Windows profiler
# build is MSBuild/vcxproj-based, not CMake (see README). curl/zstd for a
# future Windows integration are declared separately in vcpkg.json in this
# same directory, pinned to the exact same versions vendored below - bump
# both together if you ever bump one.

include(FetchContent)

# Using the manual Declare+Populate+add_subdirectory form (not the
# FetchContent_MakeAvailable() one-liner) because that convenience wrapper
# needs CMake 3.14+, and this repo's CI pins an older CMake (3.13.4) that
# the one-liner would hard-fail on at configure time.

# zstd: small, no dependencies of its own, ships an official CMake build
# (under build/cmake) - about as easy as vendoring gets. Static-linked; we
# only need the codec, not its CLI/tests.
FetchContent_Declare(zstd
  URL https://github.com/facebook/zstd/archive/refs/tags/v1.5.7.tar.gz
  URL_HASH SHA256=37d7284556b20954e56e1ca85b80226768902e2edabd3b649e9e72c0c9012ee3
)
FetchContent_GetProperties(zstd)
if(NOT zstd_POPULATED)
  FetchContent_Populate(zstd)
  # Plain set(), not set(... CACHE ... FORCE): these need to reach zstd's
  # option() calls (child directory scopes inherit the parent's variables),
  # but must NOT leak into the global CMake cache and affect unrelated
  # targets elsewhere in the dd-trace-dotnet build.
  set(ZSTD_BUILD_STATIC ON)
  set(ZSTD_BUILD_SHARED OFF)
  set(ZSTD_BUILD_PROGRAMS OFF)
  set(ZSTD_BUILD_TESTS OFF)
  set(ZSTD_BUILD_CONTRIB OFF)
  set(ZSTD_LEGACY_SUPPORT OFF)
  add_subdirectory(${zstd_SOURCE_DIR}/build/cmake ${zstd_BINARY_DIR})
endif()

# curl: built HTTP-only with all TLS backends disabled. This PoC's only
# exercised path is plain http://<agent>/profiling/v1/input (agent mode);
# agentless/HTTPS is unimplemented-in-practice regardless (see README
# "Known simplifications"), so there's nothing to lose by not vendoring a
# TLS backend too.
# Pinned to 8.19.0, not the latest (8.22.0): curl bumped its own
# cmake_minimum_required from "3.7...3.16" to a flat 3.18 starting at 8.20.0,
# which this repo's CI CMake (3.13.4) can't satisfy. 8.19.0 is the newest
# release still requiring only 3.7. Must match the curl version pinned in
# the root vcpkg.json's "overrides" (Windows side) - bump both together.
FetchContent_Declare(curl
  URL https://github.com/curl/curl/archive/refs/tags/curl-8_19_0.tar.gz
  URL_HASH SHA256=3342390f774bda7523464a13c535312962a27b026e7a05ec8dab027a8480327e
)
FetchContent_GetProperties(curl)
if(NOT curl_POPULATED)
  FetchContent_Populate(curl)
  set(BUILD_CURL_EXE OFF)      # library only, no `curl` CLI tool
  set(BUILD_SHARED_LIBS OFF)   # static-link curl into our library
  set(BUILD_STATIC_LIBS ON)
  set(CURL_ENABLE_SSL OFF)     # no TLS backend needed/vendored - agent mode is plain HTTP
  set(HTTP_ONLY ON)            # disables FTP/LDAP/SMTP/etc - overrides all CURL_DISABLE_* options
  set(CURL_USE_LIBPSL OFF)     # cookie public-suffix-list matching - unused, avoids another dependency
  set(CURL_USE_LIBSSH2 OFF)    # SCP/SFTP - unused
  set(CURL_ZLIB OFF)           # response content-encoding decompression - unused, we ignore response bodies
  set(CURL_DISABLE_INSTALL ON)
  add_subdirectory(${curl_SOURCE_DIR} ${curl_BINARY_DIR})
endif()

# Named datadog_profiling_poc (not datadog_profiling) so its default output
# filename (libdatadog_profiling_poc.so) never collides with the real
# vendored libdatadog_profiling.so - both end up linked into the same final
# Datadog.Profiler.Native.so, and an identical DT_NEEDED name between them
# would make the dynamic loader silently resolve both to whichever one it
# finds first.
file(GLOB POC_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/src/*.c")
add_library(datadog_profiling_poc SHARED ${POC_SOURCES})
target_include_directories(datadog_profiling_poc PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/include)
target_link_libraries(datadog_profiling_poc PRIVATE CURL::libcurl libzstd_static)

# M1/M2 manual smoke-test driver - a local dev tool, not installed anywhere.
add_executable(manual_smoke test/manual_smoke.c)
target_link_libraries(manual_smoke PRIVATE datadog_profiling_poc)
