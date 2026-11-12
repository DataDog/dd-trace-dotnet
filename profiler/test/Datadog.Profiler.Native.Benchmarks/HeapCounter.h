// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <cstdint>

namespace bench {
// Heap bytes currently allocated through operator new (see HeapCounter.cpp)
extern std::atomic<std::int64_t> g_liveHeapBytes;

inline std::int64_t LiveHeapBytes()
{
    return g_liveHeapBytes.load(std::memory_order_relaxed);
}
} // namespace bench
