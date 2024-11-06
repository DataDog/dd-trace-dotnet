// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "stdint.h"

// Uniquely identify a network activity based on the GUID provided by the events payload
// Only the first 12 bytes of the GUID are used to identify the activity
#pragma pack(1)
struct NetworkActivity
{
public:
    uint32_t High;
    uint32_t Middle;
    uint32_t Low;

public:
    NetworkActivity()
    {
        High = 0;
        Middle = 0;
        Low = 0;
    }

    bool operator==(const NetworkActivity& other) const = default;

private:
    // from https://www.boost.org/doc/libs/1_86_0/libs/container_hash/doc/html/hash.html#notes_hash_combine
    static size_t mix32(uint32_t x)
    {
        x ^= x >> 16;
        x *= 0x21f0aaad;
        x ^= x >> 15;
        x *= 0x735a2d97;
        x ^= x >> 15;

        return x;
    }

    static void hash_combine(size_t& seed, uint32_t v)
    {
        seed ^= mix32(static_cast<uint32_t>(seed) + 0x9e3779b9 + v);
    }

public:
    std::size_t get_hash_code() const
    {
        std::size_t seed = 0;

        hash_combine(seed, High);
        hash_combine(seed, Middle);
        hash_combine(seed, Low);
        return seed;
    }
};
#pragma pack(1)

template<>
struct std::hash<NetworkActivity>
{
    std::size_t operator()(const NetworkActivity& activity) const noexcept
    {
        return activity.get_hash_code();
    }
};
