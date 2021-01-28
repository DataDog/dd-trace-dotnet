cmake_minimum_required (VERSION 3.8)
cmake_policy(SET CMP0015 NEW)

# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Trace.ClrProfiler.Native" VERSION 1.22.1)

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

if (NOT EXISTS ${CMAKE_SOURCE_DIR}/../bin/ProfilerResources/netcoreapp2.0)
    message(FATAL_ERROR "Datadog.Trace.ClrProfiler.Loader must be build first.")
else()
    message(STATUS "Datadog.Trace.ClrProfiler.Loader was found")
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

SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})

# ******************************************************
# Dependencies
# ******************************************************

# Prepare dependencies
if (NOT EXISTS ${OUTPUT_DEPS_DIR}/json)
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/json
        COMMAND git clone --depth 1 --branch v3.3.0 https://github.com/nlohmann/json.git
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()

if (NOT EXISTS ${OUTPUT_DEPS_DIR}/re2)
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/re2
        COMMAND git clone --depth 1 --branch 2018-10-01 https://github.com/google/re2.git && cd re2 && env CXXFLAGS=\"-O3 -g -fPIC\" make
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()

if (NOT EXISTS ${OUTPUT_DEPS_DIR}/fmt)
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/fmt
        COMMAND git clone --depth 1 --branch 5.3.0 https://github.com/fmtlib/fmt.git && cd fmt && cmake -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0 . && make
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()

# Set Managed Loader folder
SET(MANAGED_LOADER_DIRECTORY ${CMAKE_SOURCE_DIR}/../bin/ProfilerResources/netcoreapp2.0)

# Set specific custom commands to embed the loader
if (ISMACOS)
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
            COMMAND touch stub.c && gcc -o stub.o -c stub.c && cp ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll Datadog.Trace.ClrProfiler.Managed.Loader.dll && ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o -sectcreate binary dll Datadog.Trace.ClrProfiler.Managed.Loader.dll stub.o
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
            COMMAND touch stub.c && gcc -o stub.o -c stub.c && cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o -sectcreate binary pdb Datadog.Trace.ClrProfiler.Managed.Loader.pdb stub.o
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
elseif(ISLINUX)
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll" Datadog.Trace.ClrProfiler.Managed.Loader.dll && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o Datadog.Trace.ClrProfiler.Managed.Loader.dll
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o Datadog.Trace.ClrProfiler.Managed.Loader.pdb
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
endif()
SET(GENERATED_OBJ_FILES
        ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
        ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
)
SET_SOURCE_FILES_PROPERTIES(
        ${GENERATED_OBJ_FILES}
        PROPERTIES
        EXTERNAL_OBJECT false
        GENERATED true
)

# ******************************************************
# Compiler options
# ******************************************************

# Sets the compiler
if(ISLINUX)
    SET (CMAKE_C_COMPILER   /usr/bin/clang)
    SET (CMAKE_CXX_COMPILER /usr/bin/clang++)
endif()

# Sets compiler options
add_compile_options(-std=c++11 -fPIC -fms-extensions)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -Wno-c++17-extensions)
if (ISMACOS)
    add_compile_options(-stdlib=libc++ -DMACOS -Wno-pragma-pack)
elseif(ISLINUX)
    add_compile_options(-DLINUX -Wno-pragmas)
endif()
if (BIT64)
    add_compile_options(-DBIT64)
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

# ******************************************************
# Define static target
# ******************************************************
add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
        class_factory.cpp
        clr_helpers.cpp
        cor_profiler_base.cpp
        cor_profiler.cpp
        il_rewriter_wrapper.cpp
        il_rewriter.cpp
        integration_loader.cpp
        integration.cpp
        logging.cpp
        metadata_builder.cpp
        miniutf.cpp
        sig_helpers.cpp
        string.cpp
        util.cpp
        calltarget_tokens.cpp
        rejit_handler.cpp
        ${GENERATED_OBJ_FILES}
)

set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")

# Define directories includes
target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
        PUBLIC lib/coreclr/src/pal/inc/rt
        PUBLIC lib/coreclr/src/pal/prebuilt/inc
        PUBLIC lib/coreclr/src/pal/inc
        PUBLIC lib/coreclr/src/inc
        PUBLIC lib/spdlog/include
        PUBLIC ${OUTPUT_DEPS_DIR}/fmt/include
        PUBLIC ${OUTPUT_DEPS_DIR}/re2
        PUBLIC ${OUTPUT_DEPS_DIR}/json/include
)

# Define linker libraries
if (ISMACOS)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        ${OUTPUT_DEPS_DIR}/re2/obj/libre2.a
        ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
    )
elseif(ISLINUX)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        ${OUTPUT_DEPS_DIR}/re2/obj/libre2.a
        ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
        -static-libgcc
        -static-libstdc++
    )
endif()

# ******************************************************
# Define shared target
# ******************************************************
if (ISMACOS)
    add_library("Datadog.Trace.ClrProfiler.Native" SHARED
        dllmain.cpp
        interop.cpp
        ${GENERATED_OBJ_FILES}
    )
else()
    add_library("Datadog.Trace.ClrProfiler.Native" SHARED
        dllmain.cpp
        interop.cpp
    )
endif()

set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES PREFIX "")

# Define linker libraries
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")
