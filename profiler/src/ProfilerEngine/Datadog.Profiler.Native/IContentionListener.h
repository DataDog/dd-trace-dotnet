// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <vector>

class IContentionListener
{
public:
    virtual ~IContentionListener() = default;

    virtual void OnContention(std::chrono::nanoseconds contentionDuration) = 0;
    virtual void OnContention(std::chrono::nanoseconds timestamp, uint32_t threadId, std::chrono::nanoseconds contentionDuration, const std::vector<uintptr_t>& stack) = 0;
    virtual void SetBlockingThread(uint64_t osThreadId) = 0;
    virtual void OnWaitStart(std::chrono::nanoseconds timestamp, uintptr_t associatedObjectId) = 0;
    virtual void OnWaitStop(std::chrono::nanoseconds timestamp) = 0;
};