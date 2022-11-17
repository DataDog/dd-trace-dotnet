// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include "GarbageCollection.h"


class IGarbageCollectionsListener
{
public:
    virtual void OnGarbageCollection(
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type,
        bool isCompacting,
        uint64_t pauseDuration,
        uint64_t totalDuration, // from start to end (includes pauses)
        uint64_t endTimestamp      // end of GC
        ) = 0;

    virtual ~IGarbageCollectionsListener() = default;
};