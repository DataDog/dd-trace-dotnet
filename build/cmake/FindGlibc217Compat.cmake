# Backing shim for a small number of glibc symbols/headers that libstdc++'s OWN headers
# opportunistically reach for (via __has_include-based feature detection) when a modern
# host's C headers are still on the include path, but that don't exist in glibc 2.17 - see
# shared/src/glibc217-compat/glibc217-compat.c for the concrete example
# (__libc_single_threaded) and why. Only used when USE_GLIBC217_SYSROOT is set (Approach A of
# docs/development/rfc-linux-x64-build-host.md).
#
# Deliberately separate from glibc-compat/FindGlibcCompat.cmake (used by the Universal
# build): that shim's strerror_r override hard-conflicts with real glibc's declared signature
# (see tracer/src/Datadog.Tracer.Native/CMakeLists.txt's comment) - reusing it here would
# just reintroduce that conflict for a problem it wasn't built to solve.
#
# Defined once here (mirroring FindGlibcCompat.cmake's own pattern), not per-subdirectory:
# add_subdirectory(tracer)/add_subdirectory(profiler) run in the same configure pass, and a
# target name can only be defined once across the whole build.
add_library(glibc217-compat OBJECT
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/glibc217-compat/glibc217-compat.c
)

set_target_properties(glibc217-compat PROPERTIES POSITION_INDEPENDENT_CODE 1)

# PRIVATE, not PUBLIC (which is what the older FindGlibcCompat.cmake uses): -std=c11 is
# needed to compile glibc217-compat.c, which is C - it has no business reaching consumers.
# PUBLIC on an OBJECT library propagates the flag to every target that links it, and here
# those are Datadog.Tracer.Native.static / Datadog.Profiler.Native.static, so every one of
# their C++ translation units was getting compiled with "-std=c11 ... -std=gnu++20". It only
# works today because clang honours the last -std wins; any ordering change would break the
# whole C++ build with "invalid argument '-std=c11' not allowed with 'C++'".
target_compile_options(glibc217-compat PRIVATE
        -std=c11
)
