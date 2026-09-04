// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog
// (https://www.datadoghq.com/). Copyright 2026 Datadog, Inc.

// Stub shadowing glibc's real sys/single_threaded.h (introduced in glibc 2.32) via -isystem
// ahead of the build host's own system include path - see glibc217-compat.c (this directory)
// for why, and build/cmake/Glibc217.cmake.x86_64 for how it's wired in. Declaration shape
// matches glibc's real header exactly.
#ifndef _SYS_SINGLE_THREADED_H
#define _SYS_SINGLE_THREADED_H

#ifdef __cplusplus
extern "C" {
#endif

extern char __libc_single_threaded;

#ifdef __cplusplus
}
#endif

#endif
