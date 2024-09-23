// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "Configuration.h"
#include "DeploymentMode.h"
#include "EnablementStatus.h"
#include "EnvironmentHelper.h"
#include "EnvironmentVariables.h"
#include "IConfiguration.h"
#include "ProfilerMockedInterface.h"
#include "SkipProfileHeuristicType.h"

#include "SsiManager.h"

#include <chrono>

using ::testing::Return;
using namespace std::chrono_literals;

TEST(SsiManagerTest, Should_NotSendProfile_When_ShortLived)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(200'000ms)); // simulate shortlive
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::ManuallyDisabled));
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).WillRepeatedly(Return(10s));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();
    manager.OnSpanCreated();
    manager.ProcessEnd();

    ASSERT_EQ(manager.GetSkipProfileHeuristic(), SkipProfileHeuristicType::ShortLived);
}

TEST(SsiManagerTest, Should_NotSendProfile_When_NoSpan)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms)); // simulate long lived
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).WillRepeatedly(Return(10s));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::SsiEnabled));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    // but no span created
    manager.ProcessStart();
    manager.ProcessEnd();

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);
    ASSERT_EQ(manager.GetSkipProfileHeuristic(), SkipProfileHeuristicType::NoSpan);
}

TEST(SsiManagerTest, Should_NotSendProfile_When_NoSpan_And_Auto_Enabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms)); // simulate long lived
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).WillRepeatedly(Return(10s));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Auto));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    // but no span created
    manager.ProcessStart();
    manager.ProcessEnd();

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);
    ASSERT_EQ(manager.GetSkipProfileHeuristic(), SkipProfileHeuristicType::NoSpan);
}

TEST(SsiManagerTest, Should_StartAsSSI_When_DeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms)); // simulate long lived

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();

    ASSERT_EQ(manager.GetDeploymentMode(), DeploymentMode::SingleStepInstrumentation);
}

TEST(SsiManagerTest, Should_StartAsManual_When_NotDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms)); // simulate long lived

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();

    ASSERT_EQ(manager.GetDeploymentMode(), DeploymentMode::Manual);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_NotDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::ManuallyDisabled));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSI)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("tracer"));
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndDisabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("tracer"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("0"));
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_DeployedAsSSIAndEnabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("profiler"));
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerEnabled(), true);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_NotDeployedAsSSIAndEnabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("1"));
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), true);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_DeployedAsSSIAndSpanAndLongLived)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::SsiEnabled));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms));  // simulate long lived

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.OnSpanCreated();
    manager.ProcessStart();

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);

    ASSERT_EQ(manager.IsProfilerStarted(), true);
    ASSERT_EQ(manager.IsProfilerEnabled(), true);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_DeployedAsSSIAndSpanAndLongLived_AndAutoEnabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Auto));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms));  // simulate long lived

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.OnSpanCreated();
    manager.ProcessStart();

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);

    ASSERT_EQ(manager.IsProfilerStarted(), true);
    ASSERT_EQ(manager.IsProfilerEnabled(), true);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndSpanOnly)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::NotSet));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(200'000ms));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();
    manager.OnSpanCreated();

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndLongLivedOnly)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::NotSet));
    // long lived
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}
