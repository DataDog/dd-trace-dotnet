// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

enum GCReason
{
    AllocSmall,
    Induced,
    LowMemory,
    Empty,
    AllocLarge,
    OutOfSpaceSOH,
    OutOfSpaceLOH,
    InducedNotForced,
    Internal,
    InducedLowMemory,
    InducedCompacting,
    LowMemoryHost,
    PMFullGC,
    LowMemoryHostBlocking
};

enum GCType
{
    NonConcurrentGC = 0,
    BackgroundGC = 1,
    ForegroundGC = 2
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