// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <vector>

class IContentionListener
{
public:
    virtual ~IContentionListener() = default;

    virtual void OnContention(double contentionDurationNs) = 0;
    virtual void OnContention(double contentionDurationNs, std::vector<uintptr_t> stack) = 0;
};