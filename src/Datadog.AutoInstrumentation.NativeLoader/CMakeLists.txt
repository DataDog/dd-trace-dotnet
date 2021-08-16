cmake_minimum_required (VERSION 3.8..3.19)
cmake_policy(SET CMP0015 NEW)

# ******************************************************
# Project definition
# ******************************************************

project("Datadog.AutoInstrumentation.NativeLoader" VERSION 0.1.0)

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
    message(STATUS "Preparing macOS build")
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

# ******************************************************
# Detect prerequisites
# ******************************************************

if (NOT EXISTS /usr/bin/git)
    message(FATAL_ERROR "GIT is required to build the project")
else()
    message(STATUS "GIT was found")
endif()

if (NOT EXISTS /usr/bin/gcc)
    message(FATAL_ERROR "GCC is required to build the project's dependencies")
else()
    message(STATUS "GCC was found")
endif()

if (NOT EXISTS /usr/bin/clang)
    message(FATAL_ERROR "CLANG is required to build the project")
else()
    message(STATUS "CLANG was found")
endif()

if (NOT EXISTS /usr/bin/clang++)
    message(FATAL_ERROR "CLANG++ is required to build the project")
else()
    message(STATUS "CLANG++ was found")
endif()

# ******************************************************
# Output folders
# ******************************************************

# Set output folders
SET(OUTPUT_BIN_DIR ${CMAKE_BINARY_DIR}/bin)
SET(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
SET(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
FILE(MAKE_DIRECTORY ${OUTPUT_BIN_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})
FILE(COPY ${CMAKE_CURRENT_SOURCE_DIR}/loader.conf DESTINATION ${CMAKE_BINARY_DIR}/bin)


SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})

# ******************************************************
# Compiler options
# ******************************************************

# Sets the compiler
if(ISLINUX)
    SET (CMAKE_C_COMPILER   /usr/bin/clang)
    SET (CMAKE_CXX_COMPILER /usr/bin/clang++)
endif()

# Sets compiler options
add_compile_options(-std=c++17 -fPIC -fms-extensions)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)
if (ISMACOS)
    add_compile_options(-stdlib=libc++ -DMACOS -Wno-pragma-pack)
elseif(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
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
add_library("Datadog.AutoInstrumentation.NativeLoader" SHARED
        cor_profiler.cpp
        cor_profiler_class_factory.cpp
        dllmain.cpp
        dynamic_dispatcher.cpp
        dynamic_instance.cpp
        miniutf.cpp
        pal.cpp
        string.cpp
        ../Datadog.Trace.ClrProfiler.Native/lib/coreclr/src/pal/prebuilt/idl/corprof_i.cpp
        ${GENERATED_OBJ_FILES}
        )

set_target_properties("Datadog.AutoInstrumentation.NativeLoader" PROPERTIES PREFIX "")

# Define directories includes
target_include_directories("Datadog.AutoInstrumentation.NativeLoader"
        PUBLIC ../Datadog.Trace.ClrProfiler.Native/lib/coreclr/src/pal/inc/rt
        PUBLIC ../Datadog.Trace.ClrProfiler.Native/lib/coreclr/src/pal/prebuilt/inc
        PUBLIC ../Datadog.Trace.ClrProfiler.Native/lib/coreclr/src/pal/inc
        PUBLIC ../Datadog.Trace.ClrProfiler.Native/lib/coreclr/src/inc
        )

# Define linker libraries
if(ISLINUX)
    target_link_libraries("Datadog.AutoInstrumentation.NativeLoader"
            -static-libgcc
            -static-libstdc++
            )
endif()