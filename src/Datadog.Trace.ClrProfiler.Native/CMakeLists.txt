cmake_minimum_required (VERSION 3.8)
cmake_policy(SET CMP0015 NEW)

project("Datadog.Trace.ClrProfiler.Native" VERSION 1.3.1)

add_compile_options(-std=c++11 -fPIC -fms-extensions)
add_compile_options(-DBIT64 -DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

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
