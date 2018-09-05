cmake_minimum_required (VERSION 3.8)

project("Datadog.Trace.ClrProfiler.Native" VERSION 0.2.0)

add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
  class_factory.cpp
  clr_helpers.cpp
  cor_profiler_base.cpp
  cor_profiler.cpp
  cs_holder.cpp
  il_rewriter.cpp
  il_rewriter_wrapper.cpp
  integration.cpp
  integration_loader.cpp
  interop.cpp
  json.hpp
  metadata_builder.cpp
  util.cpp
)
set_property(TARGET "Datadog.Trace.ClrProfiler.Native.static" PROPERTY CXX_STANDARD 17)

add_library("Datadog.Trace.ClrProfiler.Native" SHARED
  dllmain.cpp
)

target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
  PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/src/pal/inc/rt>
  PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/src/pal/prebuilt/inc>
  PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/src/pal/inc>
  PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/src/inc>
#  PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/bin/Product/$BuildOS.$BuildArch.$BuildType/inc>
)

target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")
