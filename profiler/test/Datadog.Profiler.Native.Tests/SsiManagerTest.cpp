// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "IConfiguration.h"
#include "IProfilerTelemetry.h"
#include "ProfilerMockedInterface.h"

#include "SsiManager.h"

using ::testing::Return;

TEST(SsiManagerTest, ShouldNotSendProfileWhenShortLived)
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

TEST(SsiManagerTest, ShouldNotSendProfileWhenNoSpan)
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

TEST(SsiManagerTest, ShouldStartAsSSIWhenDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(true));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.ProcessStart();

    ASSERT_EQ(telemetry.GetDeployment(), DeploymentMode::SingleStepInstrumentation);
}

TEST(SsiManagerTest, ShouldStartAsManualWhenNotDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsSsiDeployed()).WillRepeatedly(Return(false));

    ProfilerTelemetryForTest telemetry;

    SsiManager manager(configuration.get(), &telemetry);
    manager.ProcessStart();

    ASSERT_EQ(telemetry.GetDeployment(), DeploymentMode::Manual);
}