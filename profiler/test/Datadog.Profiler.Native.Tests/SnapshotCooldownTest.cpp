// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "SnapshotCooldown.h"

#include <chrono>

using namespace std::chrono_literals;

class SnapshotCooldownTest : public ::testing::Test
{
};

TEST_F(SnapshotCooldownTest, InitialStateAllowsSnapshot)
{
    SnapshotCooldown cooldown(10s);
    EXPECT_TRUE(cooldown.IsAllowed(0ns));
    EXPECT_TRUE(cooldown.IsAllowed(1ns));
}

TEST_F(SnapshotCooldownTest, OnDumpEndBlocksUntilIntervalElapses)
{
    SnapshotCooldown cooldown(10s);
    auto dumpEnd = std::chrono::nanoseconds(100s);

    cooldown.OnDumpEnd(dumpEnd);

    // Still within the cooldown
    EXPECT_FALSE(cooldown.IsAllowed(dumpEnd));
    EXPECT_FALSE(cooldown.IsAllowed(dumpEnd + 5s));
    EXPECT_FALSE(cooldown.IsAllowed(dumpEnd + 9s));

    // Exactly at boundary
    EXPECT_TRUE(cooldown.IsAllowed(dumpEnd + 10s));

    // Past the cooldown
    EXPECT_TRUE(cooldown.IsAllowed(dumpEnd + 11s));
}

TEST_F(SnapshotCooldownTest, OnCleanupDonePushesForwardWhenLaterThanDumpEnd)
{
    SnapshotCooldown cooldown(10s);
    auto dumpEnd = std::chrono::nanoseconds(100s);
    auto cleanupDone = std::chrono::nanoseconds(108s);

    cooldown.OnDumpEnd(dumpEnd);
    // next allowed = dumpEnd + 10s = 110s

    cooldown.OnCleanupDone(cleanupDone);
    // cleanup-based next = cleanupDone + 10s = 118s > 110s, so it is pushed forward

    EXPECT_FALSE(cooldown.IsAllowed(std::chrono::nanoseconds(115s)));
    EXPECT_TRUE(cooldown.IsAllowed(std::chrono::nanoseconds(118s)));
}

TEST_F(SnapshotCooldownTest, OnCleanupDoneDoesNotShortenExistingCooldown)
{
    SnapshotCooldown cooldown(10s);

    // Simulate a second dump that pushed the cooldown far forward.
    auto secondDumpEnd = std::chrono::nanoseconds(200s);
    cooldown.OnDumpEnd(secondDumpEnd);
    // next allowed = 210s

    // A late cleanup notification from the first dump arrives at 105s.
    // cleanup-based = 105s + 10s = 115s < 210s, must NOT shorten.
    auto lateFirstCleanup = std::chrono::nanoseconds(105s);
    cooldown.OnCleanupDone(lateFirstCleanup);

    EXPECT_EQ(cooldown.GetNextAllowed(), secondDumpEnd + 10s);
}

TEST_F(SnapshotCooldownTest, TestIntervalAllowsShortCooldown)
{
    // DD_INTERNAL_PROFILING_TEST_HEAPSNAPSHOT_INTERVAL feeds a short interval
    SnapshotCooldown cooldown(2s);
    auto dumpEnd = std::chrono::nanoseconds(100s);

    cooldown.OnDumpEnd(dumpEnd);

    // Blocked for only 2 seconds
    EXPECT_FALSE(cooldown.IsAllowed(dumpEnd + 1s));
    EXPECT_TRUE(cooldown.IsAllowed(dumpEnd + 2s));
}

TEST_F(SnapshotCooldownTest, MultipleDumpCyclesUpdateCooldown)
{
    SnapshotCooldown cooldown(10s);

    // First dump
    auto dump1End = std::chrono::nanoseconds(50s);
    cooldown.OnDumpEnd(dump1End);
    EXPECT_EQ(cooldown.GetNextAllowed(), dump1End + 10s);

    auto cleanup1 = std::chrono::nanoseconds(55s);
    cooldown.OnCleanupDone(cleanup1);
    // cleanup-based = 65s > 60s, pushes forward
    EXPECT_EQ(cooldown.GetNextAllowed(), cleanup1 + 10s);

    // Second dump starts after cooldown
    auto dump2End = std::chrono::nanoseconds(80s);
    cooldown.OnDumpEnd(dump2End);
    EXPECT_EQ(cooldown.GetNextAllowed(), dump2End + 10s);
}

TEST_F(SnapshotCooldownTest, ZeroIntervalAlwaysAllowed)
{
    SnapshotCooldown cooldown(0s);

    auto dumpEnd = std::chrono::nanoseconds(100s);
    cooldown.OnDumpEnd(dumpEnd);

    EXPECT_TRUE(cooldown.IsAllowed(dumpEnd));
    EXPECT_TRUE(cooldown.IsAllowed(dumpEnd + 1ns));
}
