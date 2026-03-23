include(GoogleTest)

# ******************************************************
# Compiler options
# ******************************************************

set(TEST_EXECUTABLE_NAME "Datadog.Profiler.Native.Tests")

set(TEST_OUTPUT_DIR ${OUTPUT_BUILD_DIR}/bin/${TEST_EXECUTABLE_NAME})

file(GLOB PROFILER_NATIVE_TEST_SRC CONFIGURE_DEPENDS "*.cpp")

add_executable(${TEST_EXECUTABLE_NAME}
    ${PROFILER_NATIVE_TEST_SRC}
    ../../src/ProfilerEngine/Datadog.Profiler.Native.Linux/OsSpecificApi.cpp
)

set_target_properties(${TEST_EXECUTABLE_NAME} PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
)

target_compile_options(${TEST_EXECUTABLE_NAME} PRIVATE -Wc++20-extensions)
target_compile_definitions(${TEST_EXECUTABLE_NAME} PRIVATE DD_TEST)
target_compile_options(Datadog.Profiler.Native.static PRIVATE -DDD_TEST)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
    PRIVATE ../../src/ProfilerEngine/Datadog.Profiler.Native
    PRIVATE ${googletest_SOURCE_DIR}/googlemock/include
)

target_link_libraries(${TEST_EXECUTABLE_NAME}
    profiler_compile_options
    Datadog.Profiler.Native.static
    gtest_main
    gmock_main
    -lstdc++fs
)

target_link_options(${TEST_EXECUTABLE_NAME} PRIVATE
    -static-libgcc
    -static-libstdc++
)

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
