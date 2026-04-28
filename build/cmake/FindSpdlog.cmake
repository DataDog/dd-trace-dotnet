add_library(spdlog-headers INTERFACE)

target_include_directories(spdlog-headers INTERFACE
    ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/spdlog/include
)

# We don't use spdlog's MDC (Mapped Diagnostic Context) feature, added in spdlog 1.13. Its
# header instantiates a `thread_local std::map<std::string, std::string>` inside the
# formatter, whose non-trivial destructor forces libc++abi's __cxa_thread_atexit_impl
# machinery to be linked in. In the "universal" Linux build (build/cmake/Universal.cmake.*)
# we statically link libc++abi, whose TLS variables (__cxxabiv1::dtors / dtors_alive) were
# compiled with the Local Exec model — incompatible with shared libraries. Result: link
# error "R_X86_64_TPOFF32 / R_AARCH64_TLSLE_* against __cxxabiv1::dtors[_alive] cannot be
# used with -shared".
#
# spdlog's only public switch for this is SPDLOG_NO_TLS, which is too broad — it also
# disables spdlog's TLS thread-id cache in os-inl.h, costing a gettid() syscall per log
# call. Instead, the vendored copy of pattern_formatter-inl.h is patched (via
# PatchSpdlogFile in tracer/build/_build/UpdateVendors/VendoredDependency.cs) so its 5
# MDC-related `#ifndef SPDLOG_NO_TLS` guards become
# `#if !defined(SPDLOG_NO_TLS) && !defined(DD_SPDLOG_NO_MDC)`. Defining DD_SPDLOG_NO_MDC
# below removes MDC while leaving the thread-id cache intact.
target_compile_definitions(spdlog-headers INTERFACE DD_SPDLOG_NO_MDC)
