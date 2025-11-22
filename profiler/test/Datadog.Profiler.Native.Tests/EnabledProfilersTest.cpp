// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "EnabledProfilers.h"
#include "ProfilerMockedInterface.h"

//using ::testing::_;
using ::testing::Return;
//using ::testing::ReturnRef;
//using ::testing::Throw;


TEST(EnabledProfilersTest, CheckWhenNothingIsEnabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsHeapProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsHttpProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsGcThreadsCpuTimeEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsThreadLifetimeEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsHeapSnapshotEnabled()).WillRepeatedly(Return(false));

    EnabledProfilers enabledProfilers(configuration.get(), false, false);

    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::WallTime));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Cpu));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Exceptions));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Allocations));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::LockContention));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::GC));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Heap));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Network));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::CpuGc));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::ThreadsLifetime));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::HeapSnapshot));
}

TEST(EnabledProfilersTest, CheckWhenEverythingIsEnabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHeapProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHttpProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsGcThreadsCpuTimeEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsThreadLifetimeEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHeapSnapshotEnabled()).WillRepeatedly(Return(true));

    EnabledProfilers enabledProfilers(configuration.get(), true, true);

    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::WallTime));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Cpu));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Exceptions));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Allocations));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::LockContention));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::GC));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Heap));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Network));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::CpuGc));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::ThreadsLifetime));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::HeapSnapshot));
}

TEST(EnabledProfilersTest, CheckWhenSomeAreDisabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHeapProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHttpProfilingEnabled()).WillRepeatedly(Return(true));

    EnabledProfilers enabledProfilers(configuration.get(), true, true);
    enabledProfilers.Disable(RuntimeProfiler::Cpu);
    enabledProfilers.Disable(RuntimeProfiler::Exceptions);

    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Cpu));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Exceptions));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::CpuGc));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::ThreadsLifetime));
    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::HeapSnapshot));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::WallTime));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Allocations));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::LockContention));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::GC));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Heap));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Network));
}

TEST(EnabledProfilersTest, CheckWhenDoubleDisable)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHeapProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsHttpProfilingEnabled()).WillRepeatedly(Return(true));

    EnabledProfilers enabledProfilers(configuration.get(), true, true);
    enabledProfilers.Disable(RuntimeProfiler::Cpu);
    enabledProfilers.Disable(RuntimeProfiler::Cpu);

    ASSERT_FALSE(enabledProfilers.IsEnabled(RuntimeProfiler::Cpu));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::WallTime));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Exceptions));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Allocations));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::LockContention));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::GC));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Heap));
    ASSERT_TRUE(enabledProfilers.IsEnabled(RuntimeProfiler::Network));
}
