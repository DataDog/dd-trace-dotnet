// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <vector>

class IContentionListener
{
public:
    virtual ~IContentionListener() = default;

    virtual void OnContention(double contentionDurationNs) = 0;
    virtual void OnContention(uint64_t timestamp, uint32_t threadId, double contentionDurationNs, const std::vector<uintptr_t>& stack) = 0;
    virtual void SetBlockingThread(uint64_t osThreadId) = 0;
};