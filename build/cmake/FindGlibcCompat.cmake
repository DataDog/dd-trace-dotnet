if (CMAKE_SYSTEM_NAME MATCHES "Darwin")
    add_library(glibc-compat INTERFACE)
else()
    add_library(glibc-compat OBJECT
            ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/glibc-compat/glibc-compat.c
    )

    set_target_properties(glibc-compat PROPERTIES POSITION_INDEPENDENT_CODE 1)

    target_compile_options(glibc-compat PUBLIC
            -std=c11
    )
endif()