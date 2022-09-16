
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GarbageCollectionsProvider.h"
#include "IConfiguration.h"
#include <iostream>


GarbageCollectionsProvider::GarbageCollectionsProvider(IConfiguration* configuration)
{
    _fileFolder = configuration->GetProfilesOutputDirectory();
    _garbageCollections.reserve(1024);
}

bool GarbageCollectionsProvider::GetGarbageCollections(uint8_t*& pBuffer, uint64_t& bufferSize)
{
    return false;
}

void GarbageCollectionsProvider::OnGarbageCollection(int32_t number, uint32_t generation, GCReason reason, GCType type, bool isCompacting, uint64_t pauseDuration, uint64_t timestamp)
{
    std::stringstream builder;
    builder << timestamp << " + " << number << " - " << generation << " = " << pauseDuration << " (" << type << ") " << std::endl;
    std::cout << builder.str();
}

GCInfo::GCInfo(int32_t number,
               uint32_t generation,
               GCReason reason,
               GCType type,
               bool isCompacting,
               uint64_t pauseDuration,
               uint64_t timestamp) :
    Number{number},
    Generation{generation},
    Reason{reason},
    Type{type},
    IsCompacting{isCompacting},
    PauseDuration{pauseDuration},
    Timestamp{timestamp}
{
}
