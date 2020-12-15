cmake_minimum_required (VERSION 3.8)
cmake_policy(SET CMP0015 NEW)

# Project definition
project("Datadog.Trace.ClrProfiler.Native" VERSION 1.21.2)

# Build options
option(OSX "Setting compilation for OSX" ${APPLE})

# Set output folders
SET(OUTPUT_BIN_DIR ${CMAKE_BINARY_DIR}/bin/${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
SET(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
SET(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
FILE(MAKE_DIRECTORY ${OUTPUT_BIN_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})
FILE(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})

SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR})

# Detect bitness of the build
if (CMAKE_SIZEOF_VOID_P EQUAL 8)
    SET(BIT64 true)
else()
    SET(BIT64 false)
endif()

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

if (OSX)
    message(STATUS "Preparing OSX build.")

    add_compile_options(-std=c++11 -stdlib=libc++ -fPIC -fms-extensions)
    add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX=1 -DUNICODE -DOSX)
    add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -Wno-pragma-pack)
    if (BIT64)
        add_compile_options(-DBIT64)
    endif (BIT64)

    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
            COMMAND cd ${OUTPUT_TMP_DIR} && touch stub.c && gcc -o stub.o -c stub.c && cp ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll Datadog.Trace.ClrProfiler.Managed.Loader.dll && ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o -sectcreate binary dll Datadog.Trace.ClrProfiler.Managed.Loader.dll stub.o
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
    )

    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
            COMMAND cd ${OUTPUT_TMP_DIR} && touch stub.c && gcc -o stub.o -c stub.c && cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o -sectcreate binary pdb Datadog.Trace.ClrProfiler.Managed.Loader.pdb stub.o
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
    )
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

    include_directories("Datadog.Trace.ClrProfiler.Native.static"
            ./lib/coreclr/src/pal/inc/rt
            ./lib/coreclr/src/pal/prebuilt/inc
            ./lib/coreclr/src/pal/inc
            ./lib/coreclr/src/inc
            ./lib/spdlog/include
            ${OUTPUT_DEPS_DIR}/fmt/include
            ${OUTPUT_DEPS_DIR}/re2
            ${OUTPUT_DEPS_DIR}/json/include
    )

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
        ${GENERATED_OBJ_FILES}
    )

    set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")

    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
            ${OUTPUT_DEPS_DIR}/re2/obj/libre2.a
            ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
    )

    add_library("Datadog.Trace.ClrProfiler.Native" SHARED
            dllmain.cpp
            interop.cpp
            ${GENERATED_OBJ_FILES}
    )

else()
    message(STATUS "Preparing Linux build.")

    SET (CMAKE_C_COMPILER   /usr/bin/clang)
    SET (CMAKE_CXX_COMPILER /usr/bin/clang++)

    add_compile_options(-std=c++11 -fPIC -fms-extensions)
    add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE -DLINUX)
    add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -Wno-pragmas)
    if (BIT64)
        add_compile_options(-DBIT64)
    endif (BIT64)

    add_custom_command(
        OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
        COMMAND cd ${OUTPUT_TMP_DIR} && cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll" Datadog.Trace.ClrProfiler.Managed.Loader.dll && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o Datadog.Trace.ClrProfiler.Managed.Loader.dll
        DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
    )
    add_custom_command(
        OUTPUT ${OUTPUT_TMP_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
        COMMAND cd ${OUTPUT_TMP_DIR} && cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o Datadog.Trace.ClrProfiler.Managed.Loader.pdb
        DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb ${OUTPUT_DEPS_DIR}/json ${OUTPUT_DEPS_DIR}/re2 ${OUTPUT_DEPS_DIR}/fmt
    )
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
        ${GENERATED_OBJ_FILES}
    )
    set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")
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
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        ${OUTPUT_DEPS_DIR}/re2/obj/libre2.a
        ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
        -static-libgcc
        -static-libstdc++
    )

    add_library("Datadog.Trace.ClrProfiler.Native" SHARED
        dllmain.cpp
        interop.cpp
    )

endif(OSX)

set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES PREFIX "")
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")

unset(OSX CACHE)