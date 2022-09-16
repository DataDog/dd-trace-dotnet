// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IConfiguration.h"
#include "IGarbageCollectionsProvider.h"
#include "IGarbageCollectionsListener.h"

#include <string>
#include <vector>

class IConfiguration;

class GCInfo
{
public:
    GCInfo(int32_t number,
           uint32_t generation,
           GCReason reason,
           GCType type,
           bool isCompacting,
           uint64_t pauseDuration,
           uint64_t timestamp);

public:
    int32_t Number;
    uint32_t Generation;
    GCReason Reason;
    GCType Type;
    bool IsCompacting;
    uint64_t PauseDuration;
    uint64_t Timestamp;
};

class GarbageCollectionsProvider
    :
    public IGarbageCollectionsProvider,
    public IGarbageCollectionsListener
{
public:
    GarbageCollectionsProvider(IConfiguration* configuration);

    virtual bool GetGarbageCollections(uint8_t*& pBuffer, uint64_t& bufferSize) override;
    virtual void OnGarbageCollection(int32_t number, uint32_t generation, GCReason reason, GCType type, bool isCompacting, uint64_t pauseDuration, uint64_t timestamp) override;

private:
    std::vector<GCInfo> _garbageCollections;
    std::filesystem::path _fileFolder;
};
