// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

class IGarbageCollectionsProvider
{
public:
    virtual bool GetGarbageCollections(uint8_t*& pBuffer, uint64_t& bufferSize) = 0;

    virtual ~IGarbageCollectionsProvider() = default;
};