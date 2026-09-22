// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "corprof.h"
#include <cstddef>

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
    // ETW GCRootKind::Other (manifest "Other", same byte as PerfView GCRootKind.Older) — misc roots, not static fields
    Other = 7,
    Unknown = 8
};

// Number of RootCategory enumerators (Unknown must remain the last value).
inline constexpr size_t RootCategoryCount =
    static_cast<size_t>(RootCategory::Unknown) + 1u;

inline const char* RootCategoryToString(RootCategory category)
{
    switch (category)
    {
        case RootCategory::Stack:              return "Stack";
        case RootCategory::StaticVariable:     return "StaticVariable";
        case RootCategory::Finalizer:          return "Finalizer";
        case RootCategory::Handle:             return "Handle";
        case RootCategory::Pinning:            return "Pinning";
        case RootCategory::ConditionalWeakTable: return "ConditionalWeakTable";
        case RootCategory::COM:                return "COM";
        case RootCategory::Other:              return "Other";
        case RootCategory::Unknown:            return "Unknown";
        default:                               return "Unknown";
    }
}

// Root information for collection
struct RootInfo
{
    uintptr_t address;

    RootCategory category;

    // try to work around the failing call to GetClassFromObject/GetObjectSize
    // TODO: need to find another way for BulkRootNode events because the ClassID is not provided in the payload
    ClassID classID;
    uint64_t objectSize;

    // For static roots: pointer to the UTF-16 field name from the event buffer (e.g., L"_staticOrders").
    // nullptr for non-static roots.  Valid only during the GC callback that created this RootInfo.
    const WCHAR* fieldName;

    RootInfo(uintptr_t addr, RootCategory cat, ClassID typeID, uint64_t size, const WCHAR* field = nullptr)
        :
        address(addr),
        category(cat),
        classID(typeID),
        objectSize(size),
        fieldName(field)
    {
    }
};
