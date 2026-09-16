// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog
// (https://www.datadoghq.com/). Copyright 2026 Datadog, Inc.

// Provides __libc_single_threaded (not in glibc 2.17). The matching stub header
// sys/single_threaded.h shadows the host's real one via -isystem, so libstdc++'s
// <ext/atomicity.h> __has_include check resolves here instead of pulling in a symbol
// that doesn't exist in glibc 2.17. Value 0 = "assume multi-threaded" (safe default).
char __libc_single_threaded = 0;
