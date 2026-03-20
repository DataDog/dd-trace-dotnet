include(GoogleTest)

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-Wc++20-extensions)

if(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
endif()

set(TEST_EXECUTABLE_NAME "Datadog.Linux.ApiWrapper.Tests")

set(TEST_OUTPUT_DIR ${OUTPUT_BUILD_DIR}/bin/${TEST_EXECUTABLE_NAME})

file(GLOB API_WRAPPER_NATIVE_TEST_SRC CONFIGURE_DEPENDS "*.cpp")

add_executable(${TEST_EXECUTABLE_NAME}
    ${API_WRAPPER_NATIVE_TEST_SRC}
)

set_target_properties(${TEST_EXECUTABLE_NAME} PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${TEST_OUTPUT_DIR}
)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
    PRIVATE ${googletest_SOURCE_DIR}/googlemock/include
    PRIVATE ../../../ # root repository
)

if (RUN_ASAN)
    target_compile_options(${TEST_EXECUTABLE_NAME} PRIVATE -fsanitize=address -fno-omit-frame-pointer)
    target_link_options(${TEST_EXECUTABLE_NAME} PRIVATE -fsanitize=address)
endif()

if (RUN_UBSAN)
    target_compile_options(${TEST_EXECUTABLE_NAME} PRIVATE -fsanitize=undefined -fno-omit-frame-pointer -fno-sanitize-recover=all)
    target_link_options(${TEST_EXECUTABLE_NAME} PRIVATE -fsanitize=undefined)
endif()

if (RUN_TSAN)
    target_compile_options(${TEST_EXECUTABLE_NAME} PRIVATE -fsanitize=thread -fno-omit-frame-pointer)
    target_link_options(${TEST_EXECUTABLE_NAME} PRIVATE -fsanitize=thread)
endif()

target_link_libraries(${TEST_EXECUTABLE_NAME}
    gtest_main
    gmock_main
    -ldl
)

target_link_options(${TEST_EXECUTABLE_NAME} PRIVATE
    -static-libgcc
    -static-libstdc++
)

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
