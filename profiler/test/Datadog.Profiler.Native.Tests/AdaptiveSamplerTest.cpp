// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AdaptiveSampler.h"

#include "gtest/gtest.h"

#include <thread>
#include <vector>

using namespace std::chrono_literals;

constexpr std::chrono::seconds WindowDuration = 1s;

TEST(AdaptiveSamplerTest, TestKeep)
{
    AdaptiveSampler sampler(WindowDuration, 1, 1, 1, nullptr);

    const auto initialState = sampler.GetInternalState();

    ASSERT_TRUE(sampler.Keep());

    const auto newState = sampler.GetInternalState();

    ASSERT_EQ(initialState.TestCount + 1, newState.TestCount);
    ASSERT_EQ(initialState.SampleCount + 1, newState.SampleCount);
}

TEST(AdaptiveSamplerTest, TestDrop)
{
    AdaptiveSampler sampler(WindowDuration, 1, 1, 1, nullptr);

    const auto initialState = sampler.GetInternalState();

    ASSERT_FALSE(sampler.Drop());

    const auto newState = sampler.GetInternalState();

    ASSERT_EQ(initialState.TestCount + 1, newState.TestCount);
    ASSERT_EQ(initialState.SampleCount, newState.SampleCount);
}

TEST(AdaptiveSamplerTest, TestRollWindow)
{
    AdaptiveSampler sampler(0ms, 2, 1, 1, nullptr);

    auto state = sampler.GetInternalState();

    ASSERT_EQ(0, state.TestCount);
    ASSERT_EQ(0, state.SampleCount);
    ASSERT_EQ(4, state.Budget);
    ASSERT_DOUBLE_EQ(0.0, state.TotalAverage);
    ASSERT_DOUBLE_EQ(1.0, state.Probability);

    sampler.Keep();
    sampler.Drop();

    state = sampler.GetInternalState();
    ASSERT_EQ(2, state.TestCount);
    ASSERT_EQ(1, state.SampleCount);

    sampler.RollWindow();

    state = sampler.GetInternalState();
    ASSERT_EQ(0, state.TestCount);
    ASSERT_EQ(0, state.SampleCount);
    ASSERT_EQ(1, state.Budget);
    ASSERT_DOUBLE_EQ(2.0, state.TotalAverage);
    ASSERT_DOUBLE_EQ(0.5, state.Probability);

    sampler.Keep();
    sampler.Keep();
    sampler.Drop();

    state = sampler.GetInternalState();
    ASSERT_EQ(3, state.TestCount);
    ASSERT_EQ(2, state.SampleCount);
    ASSERT_EQ(1, state.Budget);
    ASSERT_DOUBLE_EQ(2.0, state.TotalAverage);
    ASSERT_DOUBLE_EQ(0.5, state.Probability);

    sampler.RollWindow();

    state = sampler.GetInternalState();
    ASSERT_EQ(0, state.TestCount);
    ASSERT_EQ(0, state.SampleCount);
    ASSERT_EQ(0, state.Budget);
    ASSERT_DOUBLE_EQ(3.0, state.TotalAverage);
    ASSERT_DOUBLE_EQ(0.0, state.Probability);

    sampler.Drop();
    sampler.Drop();
    sampler.Drop();

    state = sampler.GetInternalState();
    ASSERT_EQ(3, state.TestCount);
    ASSERT_EQ(0, state.SampleCount);
    ASSERT_EQ(0, state.Budget);
    ASSERT_DOUBLE_EQ(3.0, state.TotalAverage);
    ASSERT_DOUBLE_EQ(0.0, state.Probability);

    sampler.RollWindow();

    state = sampler.GetInternalState();
    ASSERT_EQ(0, state.TestCount);
    ASSERT_EQ(0, state.SampleCount);
    ASSERT_EQ(2, state.Budget);
    ASSERT_DOUBLE_EQ(3.0, state.TotalAverage);
    ASSERT_DOUBLE_EQ(2.0/3.0, state.Probability);
}

TEST(AdaptiveSamplerTest, TestStop)
{
    bool callbackCalled = false;
    AdaptiveSampler sampler(0ms, 2, 1, 1, [&callbackCalled]() { callbackCalled = true; });

    sampler.RollWindow();

    ASSERT_TRUE(callbackCalled);

    callbackCalled = false;
    sampler.Stop();

    ASSERT_FALSE(callbackCalled);
}

// Regression test: _probability and _samplesBudget were declared `volatile`
// instead of `std::atomic<>`, making concurrent Sample() / RollWindow() calls
// a C++ data race (formal UB; TSAN-detectable; torn reads on ARM64).
//
// This test runs Sample() from many threads simultaneously while RollWindow()
// fires repeatedly on a separate thread.  With the volatile code this reliably
// triggers a TSAN report.  After fixing to std::atomic<> the test is clean.
TEST(AdaptiveSamplerTest, ConcurrentSampleAndRollWindow_NoDataRace)
{
    // Use a manual-roll sampler (windowDuration=0) so we control RollWindow timing.
    AdaptiveSampler sampler(0ms, 100, 3, 3, nullptr);

    std::atomic<bool> stop{false};

    // Writer thread: continuously rolls the window (updates _probability / _samplesBudget).
    std::thread writer([&]() {
        while (!stop.load(std::memory_order_relaxed))
        {
            sampler.RollWindow();
        }
    });

    // Reader threads: continuously call Sample() (reads _probability / _samplesBudget).
    const int numReaders = 4;
    const int callsPerReader = 10000;
    std::vector<std::thread> readers;
    readers.reserve(numReaders);
    for (int i = 0; i < numReaders; ++i)
    {
        readers.emplace_back([&]() {
            for (int j = 0; j < callsPerReader; ++j)
            {
                sampler.Sample();
            }
        });
    }

    for (auto& t : readers)
    {
        t.join();
    }
    stop.store(true);
    writer.join();

    // The test passes if it completes without crashing or triggering TSAN.
    // No specific count assertion: sampling is probabilistic.
    SUCCEED();
}