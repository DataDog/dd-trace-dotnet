// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CallstackProvider.h"
#include "MemoryResourceManager.h"
#include "StackSnapshotResultBuffer.h"

#include "shared/src/native-src/dd_span.hpp"

TEST(StackSnapshotResultBufferTest, CheckAddedFrames)
{
    auto p = CallstackProvider(MemoryResourceManager::GetDefault());
    auto buffer = StackSnapshotResultBuffer();
    buffer.SetCallstack(p.Get());

    std::vector<std::uintptr_t> expectedIps = {42, 21, 11, 4};
    for (auto ip : expectedIps)
    {
        buffer.AddFrame(ip);
    }

    Callstack expectedCallstack(expectedIps);
    auto callstack = buffer.GetCallstack();

    ASSERT_EQ(expectedCallstack, callstack);
}

TEST(StackSnapshotResultBufferTest, CheckAddedFakeFrame)
{
    auto p = CallstackProvider(MemoryResourceManager::GetDefault());
    auto buffer = StackSnapshotResultBuffer();
    buffer.SetCallstack(p.Get());

    std::vector<std::uintptr_t> expectedIps = {42, 21, 11, 4};

    for (auto ip : expectedIps)
    {
        ASSERT_TRUE(buffer.AddFrame(ip));
    }

    // Add a fake frame (value = 0)
    buffer.AddFakeFrame();
    expectedIps.push_back(0);

    ASSERT_EQ(expectedIps.size(), buffer.GetFramesCount());

    Callstack expectedCallstack(expectedIps);
    auto callstack = buffer.GetCallstack();

    ASSERT_EQ(expectedCallstack, callstack);
}

TEST(StackSnapshotResultBufferTest, CheckIfWeReachTheBufferLimit)
{
    auto p = CallstackProvider(MemoryResourceManager::GetDefault());
    auto buffer = StackSnapshotResultBuffer();
    buffer.SetCallstack(p.Get());

    for (auto i = 0; i < Callstack::MaxFrames; i++)
    {
        ASSERT_TRUE(buffer.AddFrame(i));
    }

    ASSERT_FALSE(buffer.AddFrame(Callstack::MaxFrames));

    ASSERT_EQ(Callstack::MaxFrames, buffer.GetFramesCount());
}