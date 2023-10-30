// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>
#include <unordered_map>
#include <stdint.h>

#include "LiveObject.h"
#include "TypeInfo.h"

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

class GcDumpState
{
public:
    GcDumpState();
    void DumpHeap();

public:
    void OnGcStart(uint32_t index, uint32_t generation, GCReason reason, GCType type);
    void OnGcEnd(uint32_t index, uint32_t generation);
    void OnTypeMapping(uint64_t id, uint32_t nameId, std::string name);
    bool AddLiveObject(uint64_t address, uint64_t typeId, uint64_t size);

private:
    bool _isStarted;
    bool _hasEnded;
    uint32_t _collectionIndex;

    //                 typeId    Name + list of instances
    std::unordered_map<uint64_t, TypeInfo> _types;
};
