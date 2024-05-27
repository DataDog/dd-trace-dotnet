// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "ServiceBase.h"

#include <future>
#include <thread>

class DummyService : public ServiceBase
{
public:
    // Inherited via ServiceBase
    const char* GetName() override
    {
        return "Dummy Service";
    }
    bool StartImpl() override
    {
        _startCallsCount++;
        return true;
    }
    bool StopImpl() override
    {
        _stopCallsCount++;
        return true;
    }

    std::atomic<int> _startCallsCount = 0;
    std::atomic<int> _stopCallsCount = 0;
};

static void process(std::shared_future<void> startSync, std::promise<void>& ready, DummyService& service)
{
    ready.set_value();
    startSync.wait();
    service.Start();
    service.Stop();
}

template <class... T>
void WaitAll(T&... args)
{
    (args.wait(), ...);
}

TEST(ServiceBaseTest, CheckThatStartAndStopCanBeCalledOnce)
{
    DummyService service;
    ASSERT_TRUE(service.Start()) << "Unable to start the service";
    ASSERT_FALSE(service.Start()) << "Service must start only once";
    ASSERT_TRUE(service.Stop()) << "Unable to stop the service";
    ASSERT_FALSE(service.Stop()) << "Service must stop only once";

    ASSERT_EQ(service._startCallsCount, 1);
    ASSERT_EQ(service._stopCallsCount, 1);
}

TEST(ServiceBaseTest, CheckStopCannotBeCalledWithoutStart)
{
    DummyService service;
    ASSERT_FALSE(service.Stop()) << "Unable to start the service";

    ASSERT_EQ(service._startCallsCount, 0) << "StartImpl must be called only once";
    ASSERT_EQ(service._stopCallsCount, 0) << "StopImpl must be called only once";
}

TEST(ServiceBaseTest, CheckServiceIsStartedOnceInMultiThreadedEnv)
{
    auto p = std::promise<void>();

    std::promise<void> ready_promise, t1_ready_promise, t2_ready_promise, t3_ready_promise, t4_ready_promise;

    std::shared_future<void> ready_future(ready_promise.get_future());

    auto fut1 = t1_ready_promise.get_future();
    auto fut2 = t2_ready_promise.get_future();
    auto fut3 = t3_ready_promise.get_future();
    auto fut4 = t4_ready_promise.get_future();

    DummyService service;
    auto result1 = std::async(std::launch::async, process, ready_future, std::ref(t1_ready_promise), std::ref(service));
    auto result2 = std::async(std::launch::async, process, ready_future, std::ref(t2_ready_promise), std::ref(service));
    auto result3 = std::async(std::launch::async, process, ready_future, std::ref(t3_ready_promise), std::ref(service));
    auto result4 = std::async(std::launch::async, process, ready_future, std::ref(t4_ready_promise), std::ref(service));

    // wait for the threads to become ready
    WaitAll(fut1, fut2, fut3, fut4);

    // start
    ready_promise.set_value();

    // wait for the threads to finish
    WaitAll(result1, result2, result3, result4);

    ASSERT_EQ(service._startCallsCount, 1);
    ASSERT_EQ(service._stopCallsCount, 1);
}