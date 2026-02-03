// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native/AutoResetEvent.h"

#include <future>

#include "gtest/gtest.h"

#define ASSERT_DURATION_LE(timeout, stmt) \
    ASSERT_TRUE(RunInThread([=] { stmt; })->WaitForCompletion(timeout));

#define ASSERT_DURATION_GE(timeout, stmt) \
    ASSERT_FALSE(RunInThread([=] { stmt; })->WaitForCompletion(timeout));


struct ThreadInfo
{
public:
    ThreadInfo() :
        _completionPromise{}
    {
        _completionFuture = _completionPromise.get_future();
    }

    bool WaitForCompletion(std::chrono::milliseconds timeout)
    {
        return _completionFuture.wait_for(timeout) == std::future_status::ready;
    }

    void SetCompleted()
    {
        ASSERT_NO_THROW(_completionPromise.set_value());
    }

private:
    std::promise<void> _completionPromise;
    std::future<void> _completionFuture;
};

class EventWrapper
{

public:
    EventWrapper(bool initialValue) :
        _event{initialValue}, _isSet{false}
    {
    }

    bool Wait(std::chrono::milliseconds timeout = InfiniteTimeout)
    {
        _isSet = _event.Wait(timeout);
        return _isSet;
    }

    void Set()
    {
        _event.Set();
    }

    bool IsSet() const
    {
        return _isSet;
    }

private:
    AutoResetEvent _event;
    bool _isSet;
};

std::shared_ptr<EventWrapper> CreateEvent(bool initialValue)
{
    return std::make_shared<EventWrapper>(initialValue);
}

std::shared_ptr<ThreadInfo> RunInThread(std::function<void()> callback)
{
    auto result = std::make_shared<ThreadInfo>();
    std::promise<void> ready;
    auto t = std::thread(
        [](std::function<void()> callback, std::promise<void>& ready, std::shared_ptr<ThreadInfo> result) {
            ready.set_value();
            callback();
            result->SetCompleted();
        },
        std::move(callback), std::ref(ready), result);
    // when test process finishes, if a non-detached thread is not joined,
    // it will crash the process.
    // So detach it and rely on asserts to bubble up an inconsistency.
    t.detach();

    auto waitReady = ready.get_future();
    waitReady.wait();
    return result;
}

TEST(AutoResetEventTest, EnsureWaitInstantlyReturnTrueIfInitialValueIsTrue)
{
    auto event = CreateEvent(true);
    ASSERT_TRUE(event->Wait());
}

TEST(AutoResetEventTest, EnsureWaitInstantlyReturnTrueIfEventIsSetBeforWaitingFor0ms)
{
    auto event = CreateEvent(false);
    event->Set();
    ASSERT_TRUE(event->Wait(0ms));
}

TEST(AutoResetEventTest, EnsureEventIsSignaledIfSetCalled)
{
    auto event = CreateEvent(false);

    auto info = RunInThread([event] { event->Wait(); });

    ASSERT_FALSE(info->WaitForCompletion(50ms));
    ASSERT_DURATION_LE(50ms, event->Set());

    ASSERT_TRUE(info->WaitForCompletion(50ms));
    ASSERT_TRUE(event->IsSet());
}

TEST(AutoResetEventTest, EnsureEventIsSignaledIfSetIsCalledWhileWaitingWithTimeout)
{
    auto event = CreateEvent(false);

    ASSERT_DURATION_GE(100ms, event->Wait(10s));
    ASSERT_DURATION_LE(50ms, event->Set());
}

TEST(AutoResetEventTest, CheckCaseWhenEventHasTimeOutButSignaledLater)
{
    auto event = CreateEvent(false);
    ASSERT_DURATION_LE(50ms, event->Wait(1ms));
    ASSERT_FALSE(event->IsSet());

    ASSERT_DURATION_LE(50ms, event->Set());
    ASSERT_DURATION_LE(50ms, event->Wait(100s));
    ASSERT_TRUE(event->IsSet());
}
#endif