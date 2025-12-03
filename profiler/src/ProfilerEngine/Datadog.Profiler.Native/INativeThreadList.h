// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

class INativeThreadList
{
public:
    virtual ~INativeThreadList() = default;

    virtual bool RegisterThread(uint32_t tid) = 0;
    virtual bool Contains(uint32_t tid) const = 0;
};