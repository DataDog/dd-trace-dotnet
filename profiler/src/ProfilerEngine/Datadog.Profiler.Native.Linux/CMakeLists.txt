# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Profiler.Native.Linux" VERSION 3.40.0)

# ******************************************************
# Output folders
# ******************************************************

set(PROFILER_BASENAME Datadog.Profiler.Native)
set(PROFILER_STATIC_LIB_NAME ${PROFILER_BASENAME}.static)
set(PROFILER_SHARED_LIB_NAME ${PROFILER_BASENAME})

# ******************************************************
# Dependencies
# ******************************************************

file(GLOB LINUX_PROFILER_SRC CONFIGURE_DEPENDS "*.cpp")

file(GLOB COMMON_PROFILER_SRC CONFIGURE_DEPENDS LIST_DIRECTORIES false "../Datadog.Profiler.Native/*.cpp")
file(GLOB EXCLUDE_DLLMAIN "../Datadog.Profiler.Native/DllMain.cpp")

list(REMOVE_ITEM COMMON_PROFILER_SRC "${EXCLUDE_DLLMAIN}")

# ******************************************************
# Define static target
# ******************************************************
add_library(${PROFILER_STATIC_LIB_NAME} STATIC
    ${LINUX_PROFILER_SRC}
    ${COMMON_PROFILER_SRC}
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
)

set_target_properties(${PROFILER_STATIC_LIB_NAME} PROPERTIES
    PREFIX ""
    ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR}
)

target_compile_options(${PROFILER_STATIC_LIB_NAME} PRIVATE -g)

# Define directories includes
target_include_directories(${PROFILER_STATIC_LIB_NAME}
    PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/../Datadog.Profiler.Native.Linux
    PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/../Datadog.Profiler.Native
    PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}
)

# Define linker libraries

if (NOT RUN_ASAN AND NOT RUN_UBSAN AND NOT RUN_TSAN)
    target_link_options(${PROFILER_STATIC_LIB_NAME} PRIVATE -Wl,--no-undefined)
endif()

datadog_target_link_libdatadog(${PROFILER_STATIC_LIB_NAME})
target_link_libraries(${PROFILER_STATIC_LIB_NAME}
    profiler_compile_options
    libunwind-lib
    coreclr
    PPDB
    spdlog-headers
    -lstdc++fs
    -pthread
    -ldl
)

add_dependencies(${PROFILER_STATIC_LIB_NAME} libdatadog-lib libunwind-lib coreclr spdlog-headers PPDB)

# ******************************************************
# Define shared target
# ******************************************************
set(dd_profiling_linker_script "${CMAKE_CURRENT_SOURCE_DIR}/datadog_profiling.version")

add_library(${PROFILER_SHARED_LIB_NAME} SHARED
    ../Datadog.Profiler.Native/DllMain.cpp
)

set_target_properties(${PROFILER_SHARED_LIB_NAME} PROPERTIES
    PREFIX ""
    ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    LINK_DEPENDS "${dd_profiling_linker_script}"
)
target_link_options(${PROFILER_SHARED_LIB_NAME} PRIVATE
    -static-libgcc
    -static-libstdc++
    -Wl,--build-id=sha1
    "LINKER:--version-script=${dd_profiling_linker_script}"
)

# Define linker libraries
target_link_libraries(${PROFILER_SHARED_LIB_NAME}
    -Wl,--whole-archive $<TARGET_FILE:${PROFILER_STATIC_LIB_NAME}> -Wl,--no-whole-archive
    -Wl,-rpath,'$ORIGIN'
    ${PROFILER_STATIC_LIB_NAME})
