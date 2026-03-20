# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Linux.ApiWrapper" VERSION 3.40.0)

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-std=c11 -fPIC -gdwarf-4)

# ******************************************************
# Output folders
# ******************************************************

set(API_WRAPPER_BASENAME Datadog.Linux.ApiWrapper)
set(API_WRAPPER_SHARED_LIB_NAME ${API_WRAPPER_BASENAME}.x64)

# ******************************************************
# Define shared target
# ******************************************************

add_library(${API_WRAPPER_SHARED_LIB_NAME} SHARED
    functions_to_wrap.c
    socket_operations.c
    common.c
)

set_target_properties(${API_WRAPPER_SHARED_LIB_NAME} PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR}
)

if (IS_ALPINE)
    target_compile_definitions(${API_WRAPPER_SHARED_LIB_NAME} PRIVATE DD_ALPINE)
endif()

# Define linker libraries
target_link_libraries(${API_WRAPPER_SHARED_LIB_NAME}
    -pthread
    -ldl
)

target_link_options(${API_WRAPPER_SHARED_LIB_NAME} PRIVATE
    -Wl,--build-id=sha1
)

set_target_properties(${API_WRAPPER_SHARED_LIB_NAME} PROPERTIES PREFIX "")
