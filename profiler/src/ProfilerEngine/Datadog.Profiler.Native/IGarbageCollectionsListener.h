// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <cstdint>
#include "GarbageCollection.h"


class IGarbageCollectionsListener
{
public:
    virtual void OnGarbageCollectionStart(
        std::chrono::nanoseconds timestamp,
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type
        ) = 0;

    virtual void OnGarbageCollectionEnd(
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type,
        bool isCompacting,
        std::chrono::nanoseconds pauseDuration,
        std::chrono::nanoseconds totalDuration, // from start to end (includes pauses)
        std::chrono::nanoseconds endTimestamp,  // end of GC
        uint64_t gen2Size,
        uint64_t lohSize,
        uint64_t pohSize,
        uint32_t memPressure) = 0;

    virtual ~IGarbageCollectionsListener() = default;
};