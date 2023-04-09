// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cor.h>  // for WCHAR
#include <cstdint>

class IAllocationsListener
{
public:
    virtual void OnAllocation(uint32_t allocationKind,
                              ClassID classId,
                              const WCHAR* typeName,
                              uintptr_t address,
                              uint64_t objectSize,
                              uint64_t allocationAmount) = 0;

    virtual ~IAllocationsListener() = default;
};