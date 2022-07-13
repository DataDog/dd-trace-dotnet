cmake_minimum_required (VERSION 3.8..3.19)
cmake_policy(SET CMP0015 NEW)

# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Trace.ClrProfiler.Native" VERSION 2.13.0)

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

if (NOT EXISTS ${CMAKE_SOURCE_DIR}/../bin/ProfilerResources/netcoreapp2.0)
    message(FATAL_ERROR "Datadog.Trace.ClrProfiler.Loader must be built first.")
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


SET(DOTNET_TRACER_REPO_ROOT_PATH ${CMAKE_SOURCE_DIR}/../../../)

# ******************************************************
# Dependencies
# ******************************************************

# Prepare dependencies
if (NOT EXISTS ${OUTPUT_DEPS_DIR}/re2)
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/re2
        COMMAND git clone --quiet --depth 1 --branch 2018-10-01 https://github.com/google/re2.git && cd re2 && env ARFLAGS=\"-r -s -c\" CXXFLAGS=\"-O3 -g -fPIC -D_GLIBCXX_USE_CXX11_ABI=0\" make -j
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()

if (NOT EXISTS ${OUTPUT_DEPS_DIR}/fmt)
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/fmt
        COMMAND git clone --quiet --depth 1 --branch 5.3.0 https://github.com/fmtlib/fmt.git && cd fmt && CXXFLAGS=\"-D_GLIBCXX_USE_CXX11_ABI=0\" && cmake -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0 . && make -j
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
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
            COMMAND touch stub.c && gcc -o stub.o -c stub.c && cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o -sectcreate binary pdb Datadog.Trace.ClrProfiler.Managed.Loader.pdb stub.o
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
elseif(ISLINUX)
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll" Datadog.Trace.ClrProfiler.Managed.Loader.dll && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o Datadog.Trace.ClrProfiler.Managed.Loader.dll
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o Datadog.Trace.ClrProfiler.Managed.Loader.pdb
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
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
        EXTERNAL_OBJECT true
        GENERATED true
)

# ******************************************************
# Compiler options
# ******************************************************

add_definitions(-D_GLIBCXX_USE_CXX11_ABI=0)

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
endif()
if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DBX86 -DHOST_X86)
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
# Define static target
# ******************************************************
add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
        class_factory.cpp
        clr_helpers.cpp
        cor_profiler_base.cpp
        cor_profiler.cpp
        il_rewriter_wrapper.cpp
        il_rewriter.cpp
        integration.cpp
        metadata_builder.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
        calltarget_tokens.cpp
        rejit_handler.cpp
        debugger_method_rewriter.cpp
        debugger_probes_instrumentation_requester.cpp
        debugger_rejit_handler_module_method.cpp
        debugger_rejit_preprocessor.cpp
        rejit_preprocessor.cpp
        rejit_work_offloader.cpp
        environment_variables_util.cpp
        method_rewriter.cpp
        tracer_tokens.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/prebuilt/idl/corprof_i.cpp
        ${GENERATED_OBJ_FILES}
)

set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")

# Define directories includes
target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/inc/rt
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/prebuilt/inc
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/inc
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/inc
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/spdlog/include
        PUBLIC ${OUTPUT_DEPS_DIR}/fmt/include
        PUBLIC ${OUTPUT_DEPS_DIR}/re2
)

# Define linker libraries
if (ISMACOS)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        ${OUTPUT_DEPS_DIR}/re2/obj/libre2.a
        ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
        ${CMAKE_DL_LIBS}
    )
elseif(ISLINUX)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        ${OUTPUT_DEPS_DIR}/re2/obj/libre2.a
        ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
        ${CMAKE_DL_LIBS}
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
