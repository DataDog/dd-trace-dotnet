// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

class IGCSuspensionsListener
{
public:
    virtual void OnSuspension(
        uint64_t timestamp,
        int32_t number,
        uint32_t generation,
        uint64_t pauseDuration) = 0;

    virtual ~IGCSuspensionsListener() = default;
};
