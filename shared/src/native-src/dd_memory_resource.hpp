// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#ifdef LINUX
#pragma clang attribute push(__attribute__((no_sanitize("returns-nonnull-attribute"))), apply_to = function)
#endif
#ifdef __has_include                 // Check if __has_include is present
#if __has_include(<memory_resource>) // Check for a standard library
#include <memory_resource>
namespace shared::pmr {
using namespace std::pmr;
}
#elif __has_include(<experimental/memory_resource>) // Check for an experimental version
#include <experimental/memory_resource>
namespace shared::pmr {
using namespace std::experimental::pmr;
}
#else // Not found at all
// cppcheck-suppress preprocessorErrorDirective
#error "Missing <memory_resource>"
#endif
#endif
#ifdef LINUX
#pragma clang attribute pop
#endif
