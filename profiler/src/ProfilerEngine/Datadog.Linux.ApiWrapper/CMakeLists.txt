# ******************************************************
# Project definition
# ******************************************************

project("Datadog.Linux.ApiWrapper" VERSION 3.37.0)

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-std=c11 -fPIC -gdwarf-4) 


if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    add_compile_options(-DDD_ALPINE)
endif()

if (ISLINUX)
    # ------------------------------------------------------
    # Hardening: make sure no target in this project ever
    # requests an executable stack.  Without this, glibc ≥2.41
    # (Debian 13 "trixie", Fedora 40, etc.) rejects the shared
    # library with:
    #   "cannot enable executable stack as shared object requires"
    # ------------------------------------------------------
    # 1. Tell the assembler to emit a .note.GNU-stack note that
    #    marks the object as **non‑exec‑stack**.
    add_compile_options("$<$<COMPILE_LANGUAGE:ASM>:-Wa,--noexecstack>")
    # 2. Instruct the linker to *clear* any stray exec‑stack flag
    #    that might still be present when it produces the final ELF.
    add_link_options(-Wl,-z,noexecstack)
endif()

# ******************************************************
# Environment detection
# ******************************************************

SET(API_WRAPPER_BASENAME Datadog.Linux.ApiWrapper)
SET(API_WRAPPER_SHARED_LIB_NAME ${API_WRAPPER_BASENAME}.x64)

SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR})

# ******************************************************
# Define shared target
# ******************************************************

add_library(${API_WRAPPER_SHARED_LIB_NAME} SHARED
    functions_to_wrap.c
    socket_operations.c
    common.c
)

# Define linker libraries
target_link_libraries(${API_WRAPPER_SHARED_LIB_NAME}
    -pthread
    -ldl
    -Wl,--build-id=sha1
)

set_target_properties(${API_WRAPPER_SHARED_LIB_NAME} PROPERTIES PREFIX "")

