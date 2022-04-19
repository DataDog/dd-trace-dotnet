// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationInfo.h"
#include "ApplicationStore.h"

#include "gtest/gtest.h"
#include "ProfilerMockedInterface.h"

using ::testing::ReturnRef;

TEST(ApplicationStoreTest, GetDefaultName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    const auto expectedServiceName = std::string("DefaultServiceName");

    EXPECT_CALL(mockConfiguration, GetServiceName()).WillRepeatedly(ReturnRef(expectedServiceName));

    ApplicationStore applicationStore(configuration.get());

    auto& name = applicationStore.GetServiceName("{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}");

    ASSERT_EQ(name, expectedServiceName);
}

TEST(ApplicationStoreTest, SetName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    ApplicationStore applicationStore(configuration.get());

    const auto runtimeId = "{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}";

    const auto expectedServiceName = "ExpectedServiceName";

    applicationStore.SetApplicationInfo(runtimeId, expectedServiceName, "", "");

    auto name = applicationStore.GetServiceName(runtimeId);

    ASSERT_EQ(name, expectedServiceName);
}