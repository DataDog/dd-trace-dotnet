// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "StackSnapshotResultBuffer.h"

TEST(StackSnapshotResultBufferTest, CheckAddedFrames)
{
    auto buffer = StackSnapshotResultBuffer();

    std::vector<std::uintptr_t> expectedIps = {42, 21, 11, 4};
    for (auto ip : expectedIps)
    {
        buffer.AddFrame(ip);
    }

    ASSERT_EQ(expectedIps.size(), buffer.GetFramesCount());

    std::vector<std::uintptr_t> ips;
    buffer.CopyInstructionPointers(ips);

    ASSERT_EQ(expectedIps, ips);
}

TEST(StackSnapshotResultBufferTest, CheckAddedFakeFrame)
{
    auto buffer = StackSnapshotResultBuffer();

    std::vector<std::uintptr_t> expectedIps = {42, 21, 11, 4};

    for (auto ip : expectedIps)
    {
        ASSERT_TRUE(buffer.AddFrame(ip));
    }

    // Add a fake frame (value = 0)
    buffer.AddFakeFrame();
    expectedIps.push_back(0);

    ASSERT_EQ(expectedIps.size(), buffer.GetFramesCount());

    std::vector<std::uintptr_t> ips;
    buffer.CopyInstructionPointers(ips);

    ASSERT_EQ(expectedIps, ips);
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

    std::vector<std::uintptr_t> ips;
    buffer.CopyInstructionPointers(ips);

    // The last frame is a fake frame
    ASSERT_EQ(0, ips.back());
}