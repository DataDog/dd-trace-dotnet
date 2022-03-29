// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "EnvironmentVariables.h"
#include "IMetricsSender.h"
#include "IMetricsSenderFactory.h"

#include "string.h"

void setenv(const shared::WSTRING& name, const shared::WSTRING& value)
{
#ifdef _WINDOWS
    SetEnvironmentVariable(name.c_str(), value.c_str());
#else
    setenv(shared::ToString(name).c_str(), shared::ToString(value).c_str(), 1);
#endif
}

void unsetenv(const shared::WSTRING& name)
{
#ifdef _WINDOWS
    SetEnvironmentVariable(name.c_str(), nullptr);
#else
    unsetenv(shared::ToString(name).c_str());
#endif
}

class IMetricsSenderFactoryTestFixture : public ::testing::Test
{
public:
    IMetricsSenderFactoryTestFixture() = default;

protected:
    void SetUp() override
    {
        unsetenv(EnvironmentVariables::OperationalMetricsEnabled);
    }
};

TEST_F(IMetricsSenderFactoryTestFixture, MustReturnNullIfEnvVarNotSet)
{
    auto envVarValue = std::getenv(shared::ToString(EnvironmentVariables::OperationalMetricsEnabled).c_str());
    EXPECT_TRUE(envVarValue == nullptr);

    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender == nullptr);
}

TEST_F(IMetricsSenderFactoryTestFixture, MustReturnNullIfEnvVarSetToZero)
{
    setenv(EnvironmentVariables::OperationalMetricsEnabled, WStr("0"));
    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender == nullptr);
}

TEST_F(IMetricsSenderFactoryTestFixture, MustReturnNullIfEnvVarValueIsNotValid)
{
    setenv(EnvironmentVariables::OperationalMetricsEnabled, WStr("NotValidValue"));
    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender == nullptr);
}

TEST_F(IMetricsSenderFactoryTestFixture, MustReturnValidPointerIfSetToOne)
{
    setenv(EnvironmentVariables::OperationalMetricsEnabled, WStr("1"));
    auto metricsSender = IMetricsSenderFactory::Create();
    EXPECT_TRUE(metricsSender != nullptr);
}