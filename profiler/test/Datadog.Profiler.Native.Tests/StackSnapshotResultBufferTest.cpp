// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "StackSnapshotResultBuffer.h"
#include "CallStack.hpp"

CallStack2 ConvertFrom(std::vector<std::uintptr_t> const& v)
{
    CallStack2 c;

    auto buffer = c.GetBuffer();
    for (auto i = 0; i < v.size(); i++)
    {
        buffer.data()[i] = v[i];
    }

    c.SetCount(v.size());

    return c;
}

TEST(StackSnapshotResultBufferTest, CheckAddedFrames)
{
    auto buffer = StackSnapshotResultBuffer();

    CallStack2 expectedCallStack = ConvertFrom(std::vector<std::uintptr_t>{42, 21, 11, 4});

    for (auto ip : expectedCallStack)
    {
        buffer.AddFrame(ip);
    }

    ASSERT_EQ(expectedCallStack.size(), buffer.GetFramesCount());

    CallStack2 ips;
    buffer.CopyInstructionPointers(ips);

    ASSERT_TRUE(expectedCallStack.SameIps(ips));
}

TEST(StackSnapshotResultBufferTest, CheckAddedFakeFrame)
{
    auto buffer = StackSnapshotResultBuffer();

    auto expectedIps = std::vector<std::uintptr_t>{42, 21, 11, 4};

    for (auto ip : expectedIps)
    {
        ASSERT_TRUE(buffer.AddFrame(ip));
    }

    // Add a fake frame (value = 0)
    buffer.AddFakeFrame();
    expectedIps.push_back(0);

    ASSERT_EQ(expectedIps.size(), buffer.GetFramesCount());

    CallStack2 expectedCallStack = ConvertFrom(expectedIps);

    CallStack2 ips;
    buffer.CopyInstructionPointers(ips);

    ASSERT_TRUE(expectedCallStack.SameIps(ips));
}

TEST(StackSnapshotResultBufferTest, CheckIfWeReachTheBufferLimitTheLastFrameIsFake)
{
    auto buffer = StackSnapshotResultBuffer();

    for (auto i = 1; i < 2049; i++)
    {
        ASSERT_TRUE(buffer.AddFrame(i));
    }

    ASSERT_FALSE(buffer.AddFrame(2049));

    ASSERT_EQ(2049, buffer.GetFramesCount());

    CallStack2 ips;
    buffer.CopyInstructionPointers(ips);

    // The last frame is a fake frame
    //ASSERT_EQ(0, *(ips.end() - 1));
    // TODO
}