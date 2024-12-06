// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
// end

#include <memory>

// Uniquely identify a network activity based on the GUID provided by the events payload
// Only the first 12 bytes of the GUID are used to identify the activity
struct NetworkActivity
{
public:
    uint32_t High;
    uint32_t Middle;
    uint32_t Low;

public:
    NetworkActivity();
    bool operator==(const NetworkActivity& other) const = default;
    size_t get_hash_code() const;

private:
    // from https://www.boost.org/doc/libs/1_86_0/libs/container_hash/doc/html/hash.html#notes_hash_combine
    static size_t mix32(uint32_t x);
    static void hash_combine(size_t& seed, uint32_t v);

public:
    // ----------------------------------------------------------------------------------------------------------------------------------------
    // from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/ActivityTracker.cs
    //
    static void WriteNibble(uint8_t*& ptr, uint8_t* endPtr, uint32_t value);
    static int AddIdToGuid(uint8_t* outPtr, int whereToAddId, uint32_t id, bool overflow = false);
    static bool GetRootActivity(LPCGUID pActivityGuid, NetworkActivity& activity, bool isRoot = true);
};

template<>
struct std::hash<NetworkActivity>
{
    std::size_t operator()(const NetworkActivity& activity) const noexcept
    {
        return activity.get_hash_code();
    }
};
