// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cor.h>  // for WCHAR
#include <cstdint>

#include <chrono>

class IAllocationsListener
{
public:
    virtual void OnAllocation(uint32_t allocationKind,
                              ClassID classId,
                              const WCHAR* typeName,
                              uintptr_t address,
                              uint64_t objectSize,
                              uint64_t allocationAmount) = 0;

    // for .NET Framework, events are received asynchronously
    // and the callstack is received as a sibling event
    // --> we cannot walk the stack of the current thread
    virtual void OnAllocation(std::chrono::nanoseconds timestamp,
                              uint32_t threadId,
                              uint32_t allocationKind,
                              ClassID classId,
                              const std::string& typeName,
                              uint64_t allocationAmount,
                              const std::vector<uintptr_t>& stack) = 0;

   // New dynamic allocation sampling for .NET 10+
   // NOTE: The sampling distribution mean is 100 KB
    virtual void OnAllocationSampled(
        uint32_t allocationKind,
        ClassID classId,
        const WCHAR* typeName,
        uintptr_t address,
        uint64_t objectSize,
        uint64_t allocationByteOffset) = 0;

    virtual ~IAllocationsListener() = default;
};