include(GoogleTest)

# ******************************************************
# Dependencies
# ******************************************************

if (NOT EXISTS ${OUTPUT_DEPS_DIR}/fmt)
    message(STATUS "clone fmt")
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/fmt
        COMMAND echo "cloning" && git clone --depth 1 --branch 5.3.0 https://github.com/DataDog/fmt.git && cd fmt && cmake -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0 . && make
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()

# ******************************************************
# Compiler options
# ******************************************************
set(CMAKE_CXX_STANDARD 17)

# Sets compiler options
add_compile_options(-fPIC -fms-extensions -stdlib=libstdc++)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -Wc++17-extensions)
add_compile_options(-DLINUX -Wno-pragmas) 

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

add_executable(${TEST_EXECUTABLE_NAME}
    HResultConverterTest.cpp
    IMetricsSenderFactoryTest.cpp
    ${PROFILER_SHARED}/HResultConverter.cpp
    ${PROFILER_SHARED}/DogstatsdService.cpp
    ${PROFILER_SHARED}/IMetricsSenderFactory.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
)

# Define directories includes
target_include_directories(${TEST_EXECUTABLE_NAME}
        PUBLIC ../../src/dotnet-runtime-coreclr/pal/inc/rt
        PUBLIC ../../src/dotnet-runtime-coreclr/pal/prebuilt/inc
        PUBLIC ../../src/dotnet-runtime-coreclr/pal/inc
        PUBLIC ../../src/dotnet-runtime-coreclr/inc
        PUBLIC ../../src/ProfilerEngine/Datadog.Profiler.Native
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}
        PUBLIC ${OUTPUT_DEPS_DIR}/fmt/include
	PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/spdlog/include
)
target_link_libraries(
  ${TEST_EXECUTABLE_NAME}
  ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
  gtest_main
  -static-libgcc
  -static-libstdc++
  -lstdc++fs
  -Wc++17-extensions
)

gtest_discover_tests(${TEST_EXECUTABLE_NAME})
