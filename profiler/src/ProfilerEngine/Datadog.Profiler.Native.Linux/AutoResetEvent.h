// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2024 Datadog, Inc.

#pragma once

#include <chrono>
#include <memory>

using namespace std::chrono_literals;

constexpr auto InfiniteTimeout = -1ms;

/// This is synchronization class between 1 or multiple producer threads (calling Set)
/// and only one consumer thread (calling Wait)
/// For now this is the scenario we need. In the future, if we want it to be more like
/// .NET AutoReset and handling more consumers, we will have to rewrite it.
class AutoResetEvent
{
public:
    explicit AutoResetEvent(bool initialState);
    ~AutoResetEvent();

    void Set();

    // Wait will block the execution of the thread until a call to Set is made
    // or until it times out.
    // [parameter] timeout : time to wait before returning.
    //   if timeout == InfiniteTimeout, it will wait until Set is called.
    //   if timeout != InfiniteTimeout, it will wait either for a call to Set either for the timeout.
    // [Return]
    //   true if Set was called, false is the call timed out.
    bool Wait(std::chrono::milliseconds timeout = InfiniteTimeout);

private:
    struct EventImpl;
    std::unique_ptr<EventImpl> _impl;
};