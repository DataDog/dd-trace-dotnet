cmake_minimum_required (VERSION 3.8)
cmake_policy(SET CMP0015 NEW)

project("Datadog.Trace.ClrProfiler.Native" VERSION 1.7.0)

add_compile_options(-std=c++11 -fPIC -fms-extensions)
add_compile_options(-DBIT64 -DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

SET(MANAGED_LOADER_DIRECTORY
    ${CMAKE_SOURCE_DIR}/../bin/ProfilerResources/netstandard1.1
)

add_custom_command(
    OUTPUT Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
    COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll" Datadog.Trace.ClrProfiler.Managed.Loader.dll && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.dll.o Datadog.Trace.ClrProfiler.Managed.Loader.dll
    DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.dll
)
add_custom_command(
    OUTPUT Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
    COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb" Datadog.Trace.ClrProfiler.Managed.Loader.pdb && ld -r -b binary -o Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o Datadog.Trace.ClrProfiler.Managed.Loader.pdb
    DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb
)
SET(GENERATED_OBJ_FILES
    ${CMAKE_BINARY_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.dll.o
    ${CMAKE_BINARY_DIR}/Datadog.Trace.ClrProfiler.Managed.Loader.pdb.o
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
    string.cpp
    util.cpp
    ${GENERATED_OBJ_FILES}
)
set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")
target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
    PUBLIC ../../lib/coreclr/src/pal/inc/rt
    PUBLIC ../../lib/coreclr/src/pal/prebuilt/inc
    PUBLIC ../../lib/coreclr/src/pal/inc
    PUBLIC ../../lib/coreclr/src/inc
    PUBLIC $<BUILD_INTERFACE:/opt/re2>
    PUBLIC $<BUILD_INTERFACE:/opt/json/include>
)
target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
    /opt/re2/obj/libre2.a
    -static-libgcc
    -static-libstdc++
)

add_library("Datadog.Trace.ClrProfiler.Native" SHARED
    dllmain.cpp
    interop.cpp
)
set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES PREFIX "")
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")
