// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "corprof.h"

// Root category enumeration
// Follow Perfview categories for consistency
enum class RootCategory : uint8_t
{
    Stack = 0,          // local variables
    StaticVariable = 1, // global/thread static variables
    Finalizer = 2,      // objects with finalizers (which are rooted until finalization)
    Handle = 3,         // strong handle
    Pinning = 4,        // pinning handle
    ConditionalWeakTable = 5,
    COM = 6,  // COM/WinRT related
    Unknown = 7
};

// Root information for collection
struct RootInfo
{
    uintptr_t address;

    RootCategory category;

    // try to work around the failing call to GetClassFromObject/GetObjectSize
    // TODO: need to find another way for BulkRootNode events because the ClassID is not provided in the payload
    ClassID classID;
    uint64_t objectSize;

    RootInfo(uintptr_t addr, RootCategory cat, ClassID typeID, uint64_t size)
        :
        address(addr),
        category(cat),
        classID(typeID),
        objectSize(size)
    {
    }
};
