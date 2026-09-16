# Compat shim for glibc symbols that don't exist in 2.17 (e.g. __libc_single_threaded).
# See glibc217-compat.c. Separate from FindGlibcCompat.cmake (strerror_r conflict).
# Defined once here (not per-subdirectory) — mirrors FindGlibcCompat.cmake's pattern.
add_library(glibc217-compat OBJECT
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/glibc217-compat/glibc217-compat.c
)

set_target_properties(glibc217-compat PROPERTIES POSITION_INDEPENDENT_CODE 1)

# PRIVATE: -std=c11 is for this C file only, not for C++ consumers that link it.
target_compile_options(glibc217-compat PRIVATE
        -std=c11
)
