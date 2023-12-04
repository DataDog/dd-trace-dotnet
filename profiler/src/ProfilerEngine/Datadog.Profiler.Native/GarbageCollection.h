// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>


// from ClrTraceEventParser.cs
enum GCType
{
    NonConcurrentGC = 0, // A 'blocking' GC.
    BackgroundGC = 1,    // A Gen 2 GC happening while code continues to run
    ForegroundGC = 2,    // A Gen 0 or Gen 1 blocking GC which is happening when a Background GC is in progress.
};

enum GCReason
{
    AllocSmall = 0,
    Induced = 1,
    LowMemory = 2,
    Empty = 3,
    AllocLarge = 4,
    OutOfSpaceSOH = 5,
    OutOfSpaceLOH = 6,
    InducedNotForced = 7,
    Internal = 8,
    InducedLowMemory = 9,
    InducedCompacting = 10,
    LowMemoryHost = 11,
    PMFullGC = 12,
    LowMemoryHostBlocking = 13
};

enum GCGlobalMechanisms
{
    None = 0x0,
    Concurrent = 0x1,
    Compaction = 0x2,
    Promotion = 0x4,
    Demotion = 0x8,
    CardBundles = 0x10
};