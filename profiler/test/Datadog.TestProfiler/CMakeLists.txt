# ******************************************************
# Project definition
# ******************************************************

project(Datadog.TestProfiler)

# Set C++ standard
set(CMAKE_CXX_STANDARD 20)

# Set output directory to align with production profiler structure
# Production: _build/DDProf-Deploy/${ARCH_BASENAME}-${ARCH_POSTFIX}
# Test:       _build/DDProf-Test/${ARCH_BASENAME}-${ARCH_POSTFIX}
SET(TEST_PROFILER_DEPLOY_DIR ${OUTPUT_BUILD_DIR}/DDProf-Test/${ARCH_BASENAME}-${ARCH_POSTFIX})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${TEST_PROFILER_DEPLOY_DIR})

# Source files
set(SOURCES
    dllmain.cpp
    TestProfilerCallback.cpp
    TestProfilerClassFactory.cpp
    Validation.cpp
)

# Create shared library
add_library(Datadog.TestProfiler SHARED ${SOURCES})

# Set output name (no lib prefix on Linux, following Datadog conventions)
set_target_properties(Datadog.TestProfiler PROPERTIES
    PREFIX ""
    OUTPUT_NAME "Datadog.TestProfiler"
)

# Include directories
target_include_directories(Datadog.TestProfiler PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_SOURCE_DIR}/../../src/ProfilerEngine/Datadog.Profiler.Native
    ${CMAKE_SOURCE_DIR}/../../../../shared/src/native-lib/coreclr/src/pal/prebuilt/inc
)

# Link against profiler static library
# This provides ManagedCodeCache, FrameStore, and logging infrastructure
target_link_libraries(Datadog.TestProfiler PRIVATE
    Datadog.Profiler.Native.static
    pthread
    dl
)

set(dd_testprofiler_linker_script "${CMAKE_CURRENT_SOURCE_DIR}/datadog_testprofiler.version")

set_target_properties(Datadog.TestProfiler PROPERTIES
    LINK_DEPENDS "${dd_testprofiler_linker_script}"
)

target_link_options(Datadog.TestProfiler PRIVATE
                    "LINKER:--version-script=${dd_testprofiler_linker_script}")
