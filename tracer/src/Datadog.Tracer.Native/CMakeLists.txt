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

project("Datadog.Tracer.Native" VERSION 3.4.1)

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
# Output folders
# ******************************************************

# Set output folders
SET(OUTPUT_BIN_DIR ${CMAKE_CURRENT_SOURCE_DIR}/build/bin)
SET(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
SET(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
FILE(MAKE_DIRECTORY ${OUTPUT_BIN_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})

SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})


# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-std=c++20 -fPIC -fms-extensions -fvisibility=hidden -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)
if (ISMACOS)
    add_compile_options(-stdlib=libc++ -DMACOS -Wno-pragma-pack -Wno-deprecated-declarations -Wno-deprecated-this-capture)
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
add_library("Datadog.Tracer.Native.static" STATIC
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
        debugger_tokens.cpp
        rejit_preprocessor.cpp
        rejit_work_offloader.cpp
        environment_variables_util.cpp
        method_rewriter.cpp
        tracer_tokens.cpp
        debugger_environment_variables_util.cpp
        debugger_probes_tracker.cpp
        iast/aspect.cpp
        iast/aspect_filter.cpp
        iast/dataflow.cpp
        iast/dataflow_aspects.cpp
        iast/dataflow_il_analysis.cpp
        iast/dataflow_il_rewriter.cpp
        iast/hardcoded_secrets_method_analyzer.cpp
        iast/iast_util.cpp
        iast/method_analyzer.cpp
        iast/method_analyzers.cpp
        iast/method_info.cpp
        iast/module_info.cpp
        iast/signature_info.cpp
        iast/signature_types.cpp
        iast/string_literal_aspect_filter.cpp
        iast/string_optimization_aspect_filter.cpp
        fault_tolerant_envionrment_variables_util.cpp
        fault_tolerant_method_duplicator.cpp
        fault_tolerant_rewriter.cpp
        fault_tolerant_tokens.cpp
        fault_tolerant_tracker.cpp
        fault_tolerant_cor_profiler_function_control.cpp
        threadUtils.cpp
        function_control_wrapper.cpp
        tracer_handler_module_method.cpp
        tracer_integration_definition.cpp
        tracer_method_rewriter.cpp
        tracer_rejit_preprocessor.cpp
)

set_target_properties("Datadog.Tracer.Native.static" PROPERTIES PREFIX "")
target_compile_definitions("Datadog.Tracer.Native.static" PUBLIC "-D_GLIBCXX_USE_CXX11_ABI=0")

# Define linker libraries
if (ISMACOS)
    target_link_libraries("Datadog.Tracer.Native.static"
        managed-loader-objs
        coreclr
        re2-lib
        spdlog-headers
        ${CMAKE_DL_LIBS}
        ${GENERATED_OBJ_FILES}
    )
elseif(ISLINUX)
    target_link_libraries("Datadog.Tracer.Native.static"
        managed-loader-objs
        coreclr
        re2-lib
        spdlog-headers
        managed-loader-objs
        ${CMAKE_DL_LIBS}
        -static-libgcc
        -static-libstdc++
        -Wl,--build-id
    )
endif()

add_dependencies("Datadog.Tracer.Native.static" coreclr re2-lib spdlog-headers managed-loader-objs)

# ******************************************************
# Define shared target
# ******************************************************
if (ISMACOS)
    add_library("Datadog.Tracer.Native" SHARED
        dllmain.cpp
        interop.cpp
    )
else()
    add_library("Datadog.Tracer.Native" SHARED
        dllmain.cpp
        interop.cpp
    )
endif()
  
set_target_properties("Datadog.Tracer.Native" PROPERTIES PREFIX "")

# Define linker libraries
target_link_libraries("Datadog.Tracer.Native" "Datadog.Tracer.Native.static")
