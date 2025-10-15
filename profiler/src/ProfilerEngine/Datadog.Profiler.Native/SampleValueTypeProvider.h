// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Sample.h"

#include <mutex>
#include <unordered_map>
#include <vector>

// Non-thread-safe class
// Must be called underlock or make it thread-safe if needed.
// For now, keep it simple
class SampleValueTypeProvider
{
public:
    using Offset = std::uintptr_t; // Use std::uintptr_t to make it work with with libdatadog which is expected int32 or int64 indices type

    SampleValueTypeProvider();

    std::vector<Offset> GetOrRegister(std::vector<SampleValueType>& valueType);
    std::vector<SampleValueType> const& GetValueTypes();

private:
    std::int8_t GetOffset(SampleValueType const& valueType);

    std::vector<SampleValueType> _sampleTypeDefinitions;

    // Incremented each time a new vector of SampleValueType is registered via GetOrRegister
    uint32_t _nextIndex = 0;
};
