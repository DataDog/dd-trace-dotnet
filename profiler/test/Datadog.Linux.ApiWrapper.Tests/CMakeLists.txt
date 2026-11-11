include(GoogleTest)


# ******************************************************
# Compiler options
# ******************************************************
set(CMAKE_CXX_STANDARD 20)

# Sets compiler options
add_compile_options(-Wc++20-extensions)

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    add_compile_options(-DDD_ALPINE)
endif()

if (RUN_ASAN)
    add_compile_options(-g -fsanitize=address -fno-omit-frame-pointer)
endif()

if (RUN_UBSAN)
    add_compile_options(-fsanitize=undefined -g -fno-omit-frame-pointer -fno-sanitize-recover=all)
endif()

if(ISLINUX)
    add_compile_options(${DD_CXX_STDLIB_COMPILE_OPTIONS} -DLINUX -Wno-pragmas)
endif()

SET(TEST_EXECUTABLE_NAME "Datadog.Linux.ApiWrapper.Tests")

SET(TEST_OUTPUT_DIR ${OUTPUT_BUILD_DIR}/bin/${TEST_EXECUTABLE_NAME})
SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR})

FILE(GLOB API_WRAPPER_NATIVE_TEST_SRC CONFIGURE_DEPENDS "*.cpp")

add_executable(${TEST_EXECUTABLE_NAME}
    ${API_WRAPPER_NATIVE_TEST_SRC}
)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
    PUBLIC ${googletest_SOURCE_DIR}/googlemock/include
    PUBLIC ../../../ # root repository
)

add_dependencies(${TEST_EXECUTABLE_NAME} gmock gtest)

if (RUN_ASAN)
    target_link_libraries(${TEST_EXECUTABLE_NAME} -fsanitize=address)
endif()

if (RUN_UBSAN)
    target_link_libraries(${TEST_EXECUTABLE_NAME} -fsanitize=undefined)
endif()

target_link_libraries(${TEST_EXECUTABLE_NAME}
  gtest_main
  gmock_main
  -static-libgcc
  -static-libstdc++
  -ldl
  -Wc++20-extensions
)

# Provides __libc_single_threaded (not in glibc 2.17). See glibc217-compat.c.
if (USE_GLIBC217_SYSROOT)
    target_link_libraries(${TEST_EXECUTABLE_NAME} glibc217-compat)
    add_dependencies(${TEST_EXECUTABLE_NAME} glibc217-compat)
endif()

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
