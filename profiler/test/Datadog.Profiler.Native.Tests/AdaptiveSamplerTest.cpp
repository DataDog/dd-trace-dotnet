// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AdaptiveSampler.h"

#include "gtest/gtest.h"

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