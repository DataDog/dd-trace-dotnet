include(GoogleTest)
# ******************************************************
# Compiler options
# ******************************************************
# Sets compiler options
add_compile_options(-fms-extensions)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -Wc++20-extensions)

if(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
endif()

if (BIT64)
    add_compile_options(-DBIT64)
    add_compile_options(-DHOST_64BIT)
endif()

if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DBX86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()

set(TEST_EXECUTABLE_NAME "Datadog.Trace.ClrProfiler.Native.Tests")

set(TEST_OUTPUT_DIR ${CMAKE_CURRENT_SOURCE_DIR}/bin/)

add_executable(${TEST_EXECUTABLE_NAME}
    cor_profiler_test.cpp
    guid_test.cpp
    pal_test.cpp
    runtimeid_store.cpp
    string_test.cpp
    environment_variable_wrapper.cpp
)

set_target_properties(${TEST_EXECUTABLE_NAME} PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
    PRIVATE ${googletest_SOURCE_DIR}/googlemock/include
    PRIVATE ${DOTNET_TRACER_REPO_ROOT_PATH}
)

target_compile_options(${TEST_EXECUTABLE_NAME} PRIVATE -Wc++20-extensions)

target_link_libraries(${TEST_EXECUTABLE_NAME}
  Datadog.Trace.ClrProfiler.Native.static
  coreclr
  gtest_main
  -lstdc++fs
)

target_link_options(${TEST_EXECUTABLE_NAME} PRIVATE
  -static-libgcc
  -static-libstdc++
)

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
