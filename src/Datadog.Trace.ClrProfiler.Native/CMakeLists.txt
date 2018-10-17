cmake_minimum_required (VERSION 3.8)

project("Datadog.Trace.ClrProfiler.Native" VERSION 0.4.0)

add_compile_options(-std=c++11 -fPIC -fms-extensions)
add_compile_options(-DBIT64 -DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
    class_factory.cpp
    clr_helpers.cpp
    cor_profiler.cpp
    cor_profiler_base.cpp
    il_rewriter.cpp
    integration.cpp
    integration_loader.cpp
    interop.cpp
    metadata_builder.cpp
    miniutf.cpp
    logging.cpp
    util.cpp
)
set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")
target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/pal/inc/rt>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/pal/prebuilt/inc>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/pal/inc>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/inc>
    PUBLIC $<BUILD_INTERFACE:/opt/re2>
    PUBLIC $<BUILD_INTERFACE:/opt/json/include>
)
target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
    /opt/re2/obj/libre2.a
)

add_library("Datadog.Trace.ClrProfiler.Native" SHARED
    dllmain.cpp
)
set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES PREFIX "")
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")
