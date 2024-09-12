// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2024 Datadog, Inc.

#pragma once

#include <chrono>
#include <memory>

class AutoResetEvent
{
public:
    explicit AutoResetEvent(bool initialState);
    ~AutoResetEvent();

    void Set();
    bool Wait(std::chrono::milliseconds timeout);

private:
    struct EventImpl;
    std::unique_ptr<EventImpl> _impl;
};