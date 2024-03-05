// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "IConfiguration.h"
#include "IProfilerTelemetry.h"
#include "ProfilerMockedInterface.h"

#include "SsiManager.h"

using ::testing::Return;

TEST(SsiManagerTest, Should_NotSendProfile_When_ShortLived)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.OnSpanCreated();
    manager.SetLifetimeDuration(-1);  // short lived

    ASSERT_FALSE(manager.ShouldSendProfile("env", "service", "runtimeId"));
    ASSERT_EQ(telemetry.GetHeuristic(), SkipProfileHeuristicType::ShortLived);
}

TEST(SsiManagerTest, Should_NotSendProfile_When_NoSpan)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.SetLifetimeDuration(1);  // long lived
    // but no span created

    ASSERT_FALSE(manager.ShouldSendProfile("env", "service", "runtimeId"));
    ASSERT_EQ(telemetry.GetHeuristic(), SkipProfileHeuristicType::NoSpan);
}

TEST(SsiManagerTest, Should_StartAsSSI_When_DeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.SetLifetimeDuration(1);  // long lived
    manager.ProcessStart();

    ASSERT_EQ(telemetry.GetDeployment(), DeploymentMode::SingleStepInstrumentation);
}

TEST(SsiManagerTest, Should_StartAsManual_When_NotDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(false));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.SetLifetimeDuration(1);  // long lived
    manager.ProcessStart();

    ASSERT_EQ(telemetry.GetDeployment(), DeploymentMode::Manual);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_NotDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(false));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);

    ASSERT_EQ(manager.IsProfilerActivated(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);

    ASSERT_EQ(manager.IsProfilerActivated(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndDisabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsProfilerEnabled()).WillRepeatedly(Return(false));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);

    ASSERT_EQ(manager.IsProfilerActivated(), false);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_DeployedAsSSIAndEnabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));
    EXPECT_CALL(mockConfiguration, IsProfilerEnabled()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);

    ASSERT_EQ(manager.IsProfilerActivated(), true);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_NotDeployedAsSSIAndEnabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(false));
    EXPECT_CALL(mockConfiguration, IsProfilerEnabled()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);

    ASSERT_EQ(manager.IsProfilerActivated(), true);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_DeployedAsSSIAndSpanAndLongLived)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.OnSpanCreated();
    manager.SetLifetimeDuration(1);  // long lived

    ASSERT_EQ(manager.IsProfilerActivated(), true);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndSpanOnly)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.OnSpanCreated();

    ASSERT_EQ(manager.IsProfilerActivated(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndLongLivedOnly)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.SetLifetimeDuration(1);  // long lived

    ASSERT_EQ(manager.IsProfilerActivated(), false);
}
