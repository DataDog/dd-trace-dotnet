// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog
// (https://www.datadoghq.com/). Copyright 2026 Datadog, Inc.

// Backing definition for sys/single_threaded.h (in this same directory), which shadows
// glibc's real header of the same name via -isystem (see build/cmake/Glibc217.cmake.x86_64).
// Only used when USE_GLIBC217_SYSROOT is set - see
// docs/development/rfc-linux-x64-build-host.md.
//
// libstdc++'s <ext/atomicity.h> (GCC 8+) does:
//   #if __has_include(<sys/single_threaded.h>)
//     return ::__libc_single_threaded;
//   #else
//     return !__gthread_active_p();
//   #endif
// __has_include is re-evaluated fresh on every file that includes it, checking whatever C
// headers are reachable AT THAT MOMENT - not baked in when libstdc++ itself was built. Since
// this build only redirects C++ header search (not C - see Glibc217.cmake.x86_64's comment
// for why that's normally fine), a modern host's own glibc >= 2.32 still exposes the real
// sys/single_threaded.h, causing libstdc++ to reference __libc_single_threaded even though
// the actual link target (glibc 2.17) has never heard of it (verified in CI: "undefined
// symbol: __libc_single_threaded", referenced from atomicity.h, from ordinary
// std::string/std::shared_ptr usage). Our stub header keeps __has_include finding
// *something* named sys/single_threaded.h, and this file backs the resulting reference.
//
// glibc's own doc comment on __libc_single_threaded: "If this variable is non-zero, then the
// current thread is the only thread in the process image. If it is zero, the process might
// be multi-threaded." Hardcoding 0 - "always assume the process might be multi-threaded" - is
// both the safe default and exactly what happens on real glibc 2.17 today: that glibc never
// had this fast-path at all, so every one of these call sites already always takes the real
// atomic-instruction path there.
char __libc_single_threaded = 0;
