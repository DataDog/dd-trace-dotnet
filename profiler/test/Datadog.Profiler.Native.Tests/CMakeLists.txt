include(GoogleTest)


# ******************************************************
# Compiler options
# ******************************************************
set(CMAKE_CXX_STANDARD 17)

# Sets compiler options
add_compile_options(-fPIC -fms-extensions)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -Wc++17-extensions)

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

SET(TEST_EXECUTABLE_NAME "Datadog.Profiler.Native.Tests")
SET(PROFILER_SHARED ${CMAKE_SOURCE_DIR}/src/ProfilerEngine/Datadog.Profiler.Native)

SET(TEST_OUTPUT_DIR ${OUTPUT_BUILD_DIR}/bin/${TEST_EXECUTABLE_NAME})
SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})

FILE(GLOB PROFILER_NATIVE_TEST_SRC CONFIGURE_DEPENDS "*.cpp")

add_executable(${TEST_EXECUTABLE_NAME}
    ${PROFILER_NATIVE_TEST_SRC}
)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
    PUBLIC ../../src/ProfilerEngine/Datadog.Profiler.Native
    PUBLIC ${googletest_SOURCE_DIR}/googlemock/include
)

add_dependencies(${TEST_EXECUTABLE_NAME} fmt gmock gtest Datadog.Profiler.Native.static)

target_link_libraries(${TEST_EXECUTABLE_NAME}
  Datadog.Profiler.Native.static
  fmt-lib
  gtest_main
  gmock_main
  -static-libgcc
  -static-libstdc++
  -lstdc++fs
  -Wc++17-extensions
)

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
