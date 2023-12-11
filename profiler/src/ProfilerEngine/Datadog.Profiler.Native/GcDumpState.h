// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>
#include <unordered_map>
#include <stdint.h>

#include "Windows.h"

#include "LiveObject.h"
#include "TypeInfo.h"
#include "GarbageCollection.h"

// TODO: this fixes the padding problem too
// #pragma pack(1)
class GcDumpState
{
public:
    GcDumpState();
    ~GcDumpState();

    void DumpHeap();
    void Clear();

public:
    void OnGcStart(uint32_t index, uint32_t generation, GCReason reason, GCType type);
    void OnGcEnd(uint32_t index, uint32_t generation);
    void OnTypeMapping(uint64_t id, uint32_t nameId, std::string name);
    bool AddLiveObject(uint64_t address, uint64_t typeId, uint64_t size);

public:
    //                 typeId    Name + list of instances
    std::unordered_map<uint64_t, TypeInfo> _types;
    HANDLE _hEventStop;

private:
    bool _isStarted;
    bool _hasEnded;
    uint16_t padding;  // this is needed to fix the stack corruption problem
    uint32_t _collectionIndex;
};
