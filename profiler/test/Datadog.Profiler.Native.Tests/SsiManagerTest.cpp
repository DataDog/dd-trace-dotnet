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

// With the introduction of Stable Configuration, the enablement is by default in Standby
// So, it is needed to do some tests with and without Stable Configuration.
//
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

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);
    // check that short lived timer has been started in ProcessStart
    ASSERT_EQ(manager.GetSkipProfileHeuristic(), SkipProfileHeuristicType::NoSpan);
}

TEST(SsiManagerTest, Should_StartAsSSI_When_DeployedAsSSI_WithStableConfiguration)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms)); // simulate long lived

    // with Stable Configuration (which is the default)

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);

    // check that short lived timer has been started in ProcessStart even with Stable Configuration when deployed via SSI
    ASSERT_EQ(manager.GetSkipProfileHeuristic(), SkipProfileHeuristicType::NoSpan);
}

TEST(SsiManagerTest, Should_StartAsManual_When_NotDeployedAsSSI)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms)); // simulate long lived
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::ManuallyEnabled));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();

    ASSERT_EQ(manager.IsProfilerEnabled(), true);
    ASSERT_EQ(manager.IsProfilerStarted(), true);
}

TEST(SsiManagerTest, Should_NotStart_When_NotDeployAsSSI_WithStableConfiguration)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    // with Stable Configuration (which is default)
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Standby));
    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();

    ASSERT_EQ(manager.IsProfilerEnabled(), false);
    ASSERT_EQ(manager.IsProfilerStarted(), false);
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
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::NotSet));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSI_WithStableConfiguration)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Standby));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndDisabled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::ManuallyDisabled));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndEvenWithInjectionContainsProfiler)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("profiler"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerEnabled(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndEvenWithInjectionContainsProfiler_WithStableConfiguration)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("profiler"));
    // with Stable Configuration (which is default)
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerEnabled(), false);
}

TEST(SsiManagerTest, Should_ProfilerBeActivated_When_NotDeployedAsSSIAndEnabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), true);
}

TEST(SsiManagerTest, Should_ProfilerBeNotActivated_When_NotDeployedAsSSIAndEnabled_WithStableConfiguration)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("1"));
    // with Stable Configuration (which is default)
    auto configuration = Configuration{};

    SsiLifetimeForTest lifetime;

    SsiManager manager(&configuration, &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
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

TEST(SsiManagerTest, Should_ProfilerBeNotActivated_When_DeployedAsSSIAndSpanAndLongLived_AndAutoEnabled_WithStableConfiguration)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    // with Stable Configuration should be in Standby mode
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Standby));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms));  // simulate long lived

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.OnSpanCreated();
    manager.ProcessStart();

    // wait for the timer to finish
    std::this_thread::sleep_for(100ms);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
    ASSERT_EQ(manager.IsProfilerEnabled(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndSpanOnly)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Auto));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(200'000ms));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();
    manager.OnSpanCreated();

    ASSERT_EQ(manager.IsProfilerStarted(), false);
    ASSERT_EQ(manager.IsProfilerEnabled(), true);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndSpanOnly_WithStableConfiguration)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Standby));
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(200'000ms));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);
    manager.ProcessStart();
    manager.OnSpanCreated();

    ASSERT_EQ(manager.IsProfilerStarted(), false);
    ASSERT_EQ(manager.IsProfilerEnabled(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndLongLivedOnly)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Auto));
    // long lived
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}

TEST(SsiManagerTest, Should_ProfilerNotBeActivated_When_DeployedAsSSIAndLongLivedOnly_WithStableConfiguration)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    EXPECT_CALL(mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Standby));
    // long lived
    EXPECT_CALL(mockConfiguration, GetSsiLongLivedThreshold()).WillRepeatedly(Return(1ms));

    SsiLifetimeForTest lifetime;

    SsiManager manager(configuration.get(), &lifetime);

    ASSERT_EQ(manager.IsProfilerStarted(), false);
}
