include(GoogleTest)
# ******************************************************
# Compiler options
# ******************************************************
set(CMAKE_CXX_STANDARD 20)

# Sets compiler options
add_compile_options(-std=c++20 -fms-extensions)
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

SET(TEST_EXECUTABLE_NAME "Datadog.Tracer.Native.Tests")
# ======================= change the output dir
SET(OUTPUT_BIN_DIR ${CMAKE_CURRENT_SOURCE_DIR}/bin)

SET(TEST_OUTPUT_DIR ${CMAKE_CURRENT_SOURCE_DIR}/bin/)
SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})


add_executable(${TEST_EXECUTABLE_NAME}
    pch.cpp
    integration_test.cpp
    version_struct_test.cpp
    iast_util_test.cpp
)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
    PUBLIC ${googletest_SOURCE_DIR}/googlemock/include
)

add_dependencies(${TEST_EXECUTABLE_NAME} gtest Datadog.Tracer.Native.static coreclr)

target_link_libraries(${TEST_EXECUTABLE_NAME}
  Datadog.Tracer.Native.static
  coreclr
  gtest_main
  -static-libgcc
  -static-libstdc++
  -lstdc++fs
  -Wc++20-extensions
)

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
