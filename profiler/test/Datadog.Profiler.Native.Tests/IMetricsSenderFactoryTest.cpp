// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "EnvironmentHelper.h"
#include "EnvironmentVariables.h"
#include "IMetricsSender.h"
#include "IMetricsSenderFactory.h"

#include "shared/src/native-src/util.h"

TEST(IMetricsSenderFactoryTest, MustReturnNullIfEnvVarNotSet)
{    
    auto envVarValue = shared::GetEnvironmentValue(EnvironmentVariables::OperationalMetricsEnabled);
    EXPECT_TRUE(envVarValue.length() == 0);

    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender == nullptr);
}

TEST(IMetricsSenderFactoryTest, MustReturnNullIfEnvVarSetToZero)
{
    EnvironmentHelper::EnvironmentVariable er(EnvironmentVariables::OperationalMetricsEnabled, WStr("0"));
    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender == nullptr);
}

TEST(IMetricsSenderFactoryTest, MustReturnNullIfEnvVarValueIsNotValid)
{
    EnvironmentHelper::EnvironmentVariable er(EnvironmentVariables::OperationalMetricsEnabled, WStr("NotValidValue"));
    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender == nullptr);
}

TEST(IMetricsSenderFactoryTest, MustReturnValidPointerIfSetToOne)
{
    EnvironmentHelper::EnvironmentVariable er(EnvironmentVariables::OperationalMetricsEnabled, WStr("1"));
    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender != nullptr);
}