// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2024 Datadog, Inc.

#pragma once

#include <chrono>
#include <memory>

using namespace std::chrono_literals;

constexpr auto InfiniteTimeout = -1ms;

class AutoResetEvent
{
public:
    explicit AutoResetEvent(bool initialState);
    ~AutoResetEvent();

    void Set();
    bool Wait(std::chrono::milliseconds timeout = InfiniteTimeout);

private:
    struct EventImpl;
    std::unique_ptr<EventImpl> _impl;
};