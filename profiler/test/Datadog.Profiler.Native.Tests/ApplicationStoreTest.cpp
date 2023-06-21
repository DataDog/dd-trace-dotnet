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

    const std::string expectedServiceName = "DefaultServiceName";
    const std::string expectedVersion = "DefaultVersion";
    const std::string expectedEnvironment = "DefaultEnvironment";

    EXPECT_CALL(mockConfiguration, GetServiceName()).WillRepeatedly(ReturnRef(expectedServiceName));
    EXPECT_CALL(mockConfiguration, GetVersion()).WillRepeatedly(ReturnRef(expectedVersion));
    EXPECT_CALL(mockConfiguration, GetEnvironment()).WillRepeatedly(ReturnRef(expectedEnvironment));

    ApplicationStore applicationStore(configuration.get());

    const auto& info = applicationStore.GetApplicationInfo("{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}");

    ASSERT_EQ(info.ServiceName, expectedServiceName);
    ASSERT_EQ(info.Version, expectedVersion);
    ASSERT_EQ(info.Environment, expectedEnvironment);
}

TEST(ApplicationStoreTest, SetName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    ApplicationStore applicationStore(configuration.get());

    const auto runtimeId = "{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}";

    const auto expectedApplicationInfo = ApplicationInfo
    {
        "ExpectedServiceName",
        "ExpectedEnvironment",
        "ExpectedVersion",
    };

    applicationStore.SetApplicationInfo(
        runtimeId,
        expectedApplicationInfo.ServiceName,
        expectedApplicationInfo.Environment,
        expectedApplicationInfo.Version);

    const auto& info = applicationStore.GetApplicationInfo(runtimeId);

    ASSERT_EQ(info.Environment, expectedApplicationInfo.Environment);
    ASSERT_EQ(info.ServiceName, expectedApplicationInfo.ServiceName);
    ASSERT_EQ(info.Version, expectedApplicationInfo.Version);
}