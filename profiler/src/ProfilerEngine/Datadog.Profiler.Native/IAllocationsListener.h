// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cor.h>  // for WCHAR
#include <cstdint>

class IAllocationsListener
{
public:
    virtual void OnAllocation(uint32_t allocationKind,
                              const WCHAR* TypeName,
                              uintptr_t Address,
                              uint64_t ObjectSize) = 0;

    virtual ~IAllocationsListener() = default;
};