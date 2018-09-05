cmake_minimum_required (VERSION 3.8)

project("Datadog.Trace.ClrProfiler.Native" VERSION 0.2.0)

add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
            ClassFactory.cpp
            clr_helpers.cpp
            cor_profiler_base.cpp
            cor_profiler.cpp
            CSHolder.cpp
            ILRewriter.cpp
            ILRewriterWrapper.cpp
            integration.cpp
            integration_loader.cpp
            Interop.cpp
            json.hpp
            metadata_builder.cpp
            util.cpp
)

add_library("Datadog.Trace.ClrProfiler.Native.shared" SHARED
            dllmain.cpp
)

target_include_directories("Datadog.Trace.ClrProfiler.Native.static"
                           PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/src/inc>
                           PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/../../lib/coreclr/src/pal/prebuilt/inc>
)

target_link_libraries("Datadog.Trace.ClrProfiler.Native.shared" "Datadog.Trace.ClrProfiler.Native.static")


# TODO: Add tests and install targets if needed.
