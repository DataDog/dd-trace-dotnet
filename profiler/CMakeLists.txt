cmake_minimum_required (VERSION 3.13.4)
cmake_policy(SET CMP0015 NEW)

# ******************************************************
# Environment detection
# ******************************************************

# Detect operating system
if (CMAKE_SYSTEM_NAME MATCHES "Windows")
    message(FATAL_ERROR "Windows builds are not supported using CMAKE. Please use Visual Studio")
    SET(ISWINDOWS true)
elseif (CMAKE_SYSTEM_NAME MATCHES "Linux")
    message(STATUS "Preparing Linux build")
    SET(ISLINUX true)
elseif (CMAKE_SYSTEM_NAME MATCHES "Darwin")
    message(FATAL_ERROR "MACOS builds are not supported yet.")
    SET(ISMACOS true)
endif()

# Detect bitness of the build
if (CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(STATUS "Setting compilation for 64bits processor")
    SET(BIT64 true)
endif()

# Detect architecture
if (CMAKE_SYSTEM_PROCESSOR STREQUAL x86_64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL amd64)
    message(STATUS "Architecture is x64/AMD64")
    SET(ISAMD64 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL x86 OR CMAKE_SYSTEM_PROCESSOR STREQUAL i686)
    message(STATUS "Architecture is x86")
    SET(ISX86 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
    message(STATUS "Architecture is ARM64")
    SET(ISARM64 true)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL armv7l OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm)
    message(STATUS "Architecture is ARM")
    SET(ISARM true)
endif()

# Out of source build directory
SET(OUTPUT_BUILD_DIR ${CMAKE_CURRENT_SOURCE_DIR}/_build)

if (DEFINED RUN_ANALYSIS AND NOT RUN_ANALYSIS EQUAL 0)
    SET(CMAKE_EXPORT_COMPILE_COMMANDS 1)
endif()

# ******************************************************
# Detect prerequisites
# ******************************************************

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

find_program(FOUND_GIT "git")

if (NOT FOUND_GIT)
    message(FATAL_ERROR "GIT is required to build the project")
else()
    message(STATUS "GIT was found")
endif()

if (DEFINED RUN_ANALYSIS AND RUN_ANALYSIS EQUAL 1)
    find_program(FOUND_CLANGTIDY clang-tidy)
    if (NOT FOUND_CLANGTIDY)
        message(FATAL_ERROR "clang-tidy is required to build and analyze the project")
    else()
        message(STATUS "clang-tidy was found")
    endif()
endif()

add_definitions("-D_GLIBCXX_USE_CXX11_ABI=0")

# ******************************************************
# Output folders
# ******************************************************

# Deployment directory

if (ISAMD64)
    SET(ARCH_POSTFIX "x64")
elseif (ISARM64)
    SET(ARCH_POSTFIX "arm64")
elseif (ISARM)
    SET(ARCH_POSTFIX "arm")
else()
    SET(ARCH_POSTFIX "x86")
endif()

SET(ARCH_BASENAME "linux")
if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    SET(IS_ALPINE)
    SET(ARCH_BASENAME "${ARCH_BASENAME}-musl")
endif()

SET(DEPLOY_DIR ${OUTPUT_BUILD_DIR}/DDProf-Deploy/${ARCH_BASENAME}-${ARCH_POSTFIX})

# Set output folders
SET(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
SET(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
FILE(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})

add_subdirectory(src/ProfilerEngine/Datadog.Profiler.Native.Linux)
add_subdirectory(src/ProfilerEngine/Datadog.Linux.ApiWrapper)
enable_testing()
add_subdirectory(test)

add_custom_target(all-profiler)
add_dependencies(all-profiler Datadog.Profiler.Native Datadog.Linux.ApiWrapper.x64 Datadog.Profiler.Native.Tests)
