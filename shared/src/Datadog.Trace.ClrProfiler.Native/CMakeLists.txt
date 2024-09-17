cmake_minimum_required (VERSION 3.8..3.19)
cmake_policy(SET CMP0015 NEW)

# macOS uses 3.30 which deprecates FetchContent_Populate in favor of FetchContent_MakeAvailable,
# but we're using 3.13.4 on Linux, which doesn't have FetchContent_MakeAvailable

if(POLICY CMP0169)
    cmake_policy(SET CMP0169 OLD)
endif()

# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Trace.ClrProfiler.Native" VERSION 3.3.1)

if (UNIVERSAL)
   find_package(GlibcCompat REQUIRED)
   message(STATUS "Glibc Compat objects")
endif()

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


# ******************************************************
# Detect prerequisites
# ******************************************************

find_program(FOUND_GIT "git")
if (NOT FOUND_GIT)
    message(FATAL_ERROR "GIT is required to build the project")
else()
    message(STATUS "GIT was found")
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
# Output folders
# ******************************************************

# Set output folders
SET(OUTPUT_BIN_DIR ${CMAKE_CURRENT_SOURCE_DIR}/bin)
SET(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
SET(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
FILE(MAKE_DIRECTORY ${OUTPUT_BIN_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})
FILE(COPY ${CMAKE_CURRENT_SOURCE_DIR}/loader.conf DESTINATION ${OUTPUT_BIN_DIR})


SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-std=c++20 -fPIC -fms-extensions -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

if (UNIVERSAL)
    add_compile_options(-stdlib=libc++ -DUNIVERSAL)
elseif (ISMACOS)
    add_compile_options(-stdlib=libc++ -DMACOS -Wno-pragma-pack -Wno-deprecated-declarations -fvisibility=hidden)
else()
    add_compile_options(-stdlib=libstdc++)
endif()

if(ISLINUX)
    add_compile_options(-DLINUX -Wno-pragmas)
endif()

if (BIT64)
    add_compile_options(-DBIT64 -DHOST_64BIT)
else()
    add_compile_options(-DBIT86 -DHOST_X86)
endif()
if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DX86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()

# ******************************************************
# Suppress Warning on MacOS
# ******************************************************

# Only necessary with cmake 3.19.x on macos
# See https://stackoverflow.com/questions/4929255/building-static-libraries-on-mac-using-cmake-and-gcc#answer-4953904

if (ISMACOS)
    SET(CMAKE_C_ARCHIVE_CREATE   "<CMAKE_AR> Scr <TARGET> <LINK_FLAGS> <OBJECTS>")
    SET(CMAKE_CXX_ARCHIVE_CREATE "<CMAKE_AR> Scr <TARGET> <LINK_FLAGS> <OBJECTS>")
    SET(CMAKE_C_ARCHIVE_FINISH   "<CMAKE_RANLIB> -no_warning_for_no_symbols -c <TARGET>")
    SET(CMAKE_CXX_ARCHIVE_FINISH "<CMAKE_RANLIB> -no_warning_for_no_symbols -c <TARGET>")
endif()

# ******************************************************
# Define target
# ******************************************************
add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
        cor_profiler.cpp
        process_helper.cpp
        single_step_guard_rails.cpp
        cor_profiler_class_factory.cpp
        dynamic_dispatcher.cpp
        dynamic_instance.cpp
        runtimeid_store.cpp
        instrumented_assembly_generator/instrumented_assembly_generator_cor_profiler_function_control.cpp
        instrumented_assembly_generator/instrumented_assembly_generator_cor_profiler_info.cpp
        instrumented_assembly_generator/instrumented_assembly_generator_metadata_interfaces.cpp
        instrumented_assembly_generator/member_signature.cpp
        instrumented_assembly_generator/method_signature.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/dynamic_library_base.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/dynamic_com_library.cpp
        )

target_compile_definitions("Datadog.Trace.ClrProfiler.Native.static" PRIVATE "-D_GLIBCXX_USE_CXX11_ABI=0")


find_library(LIBDL dl)

# Define linker libraries
if (ISMACOS)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        $<$<BOOL:${LIBDL}>:dl>
        coreclr
        spdlog-headers
    )
elseif(ISLINUX)
    set(dd_profiling_linker_script "${CMAKE_CURRENT_SOURCE_DIR}/native_loader.version")
    find_library(LIBPTHREAD pthread)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        -static-libgcc
        -static-libstdc++
        $<$<BOOL:${LIBPTHREAD}>:pthread>
        $<$<BOOL:${LIBDL}>:dl>
        coreclr
        spdlog-headers
        -Wl,--build-id
        -Wl,-version-script=${dd_profiling_linker_script}
        -Wl,--no-undefined
    )
endif()

set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")

add_dependencies("Datadog.Trace.ClrProfiler.Native.static" coreclr spdlog-headers)

if (UNIVERSAL)
   add_dependencies("Datadog.Trace.ClrProfiler.Native.static" glibc-compat)
endif()

# ******************************************************
# Define shared target
# ******************************************************
add_library("Datadog.Trace.ClrProfiler.Native" SHARED
    dllmain.cpp
    exported_functions.cpp
)
  
set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES PREFIX "")

# Define linker libraries
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")

if (UNIVERSAL)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native"
        glibc-compat
        -flto
        )
endif()
