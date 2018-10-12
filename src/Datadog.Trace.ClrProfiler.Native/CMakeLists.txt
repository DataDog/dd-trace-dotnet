cmake_minimum_required (VERSION 3.8)

project("Datadog.Trace.ClrProfiler.Native" VERSION 0.3.2)

add_compile_options(-stdlib=libc++ -std=c++17 -fPIC -fms-extensions -DBIT64 -DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-pragma-pack)

add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
    class_factory.cpp
    clr_helpers.cpp
    cor_profiler.cpp
    cor_profiler_base.cpp
    cs_holder.cpp
    il_rewriter_wrapper.cpp
    il_rewriter.cpp
    integration.cpp
    integration_loader.cpp
    interop.cpp
    logging.cpp
    metadata_builder.cpp
    util.cpp
)
set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES PREFIX "")
target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
    PUBLIC $<BUILD_INTERFACE:/opt/spdlog/include>
    PUBLIC $<BUILD_INTERFACE:/opt/json/include>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/pal/inc/rt>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/pal/prebuilt/inc>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/pal/inc>
    PUBLIC $<BUILD_INTERFACE:/opt/coreclr/src/inc>
)

add_library("Datadog.Trace.ClrProfiler.Native" SHARED
    dllmain.cpp
)
set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES PREFIX "")
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")
