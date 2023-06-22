// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationStore.h"
#include "ApplicationInfo.h"

#include "ProfilerMockedInterface.h"
#include "gtest/gtest.h"

using ::testing::ReturnRef;

TEST(ApplicationStoreTest, GetDefaultName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    const std::string expectedServiceName = "DefaultServiceName";
    const std::string expectedVersion = "DefaultVersion";
    const std::string expectedEnvironment = "DefaultEnvironment";
    const std::string expectedGitRepository = "DefaultGitRepository";
    const std::string expectedGitCommitSha = "DefaultGitCommitSha";

    EXPECT_CALL(mockConfiguration, GetServiceName()).WillRepeatedly(ReturnRef(expectedServiceName));
    EXPECT_CALL(mockConfiguration, GetVersion()).WillRepeatedly(ReturnRef(expectedVersion));
    EXPECT_CALL(mockConfiguration, GetEnvironment()).WillRepeatedly(ReturnRef(expectedEnvironment));
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(expectedGitRepository));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(expectedGitCommitSha));

    ApplicationStore applicationStore(configuration.get());

    const auto& info = applicationStore.GetApplicationInfo("{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}");

    ASSERT_EQ(info.ServiceName, expectedServiceName);
    ASSERT_EQ(info.Version, expectedVersion);
    ASSERT_EQ(info.Environment, expectedEnvironment);
    ASSERT_EQ(info.RepositoryUrl, expectedGitRepository);
    ASSERT_EQ(info.CommitSha, expectedGitCommitSha);
}

TEST(ApplicationStoreTest, CheckGitMetadataIfSetGitMetadataIsNotCalled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    std::string expectedGitRepositoryUrl = "ExpectedGitRepositoryUrl";
    std::string expectedGitCommitSha = "ExpectedGitCommitSha";
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(expectedGitRepositoryUrl));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(expectedGitCommitSha));

    ApplicationStore applicationStore(configuration.get());

    const auto runtimeId = "{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}";

    const auto expectedApplicationInfo = ApplicationInfo{
        "ExpectedServiceName",
        "ExpectedEnvironment",
        "ExpectedVersion",
        expectedGitRepositoryUrl,
        expectedGitCommitSha};

    applicationStore.SetApplicationInfo(
        runtimeId,
        expectedApplicationInfo.ServiceName,
        expectedApplicationInfo.Environment,
        expectedApplicationInfo.Version);

    auto const& info = applicationStore.GetApplicationInfo(runtimeId);

    ASSERT_EQ(info.Environment, expectedApplicationInfo.Environment);
    ASSERT_EQ(info.ServiceName, expectedApplicationInfo.ServiceName);
    ASSERT_EQ(info.Version, expectedApplicationInfo.Version);
    ASSERT_EQ(info.RepositoryUrl, expectedApplicationInfo.RepositoryUrl);
    ASSERT_EQ(info.CommitSha, expectedApplicationInfo.CommitSha);
}

TEST(ApplicationStoreTest, MakeSureCallToSetGitMetadataOverrideThePreviousValue)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    std::string randomRepoUrl = "RandomRepoUrl";
    std::string randomCommitSha = "RandomCommitSha";
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(randomRepoUrl));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(randomCommitSha));

    ApplicationStore applicationStore(configuration.get());

    const auto runtimeId = "{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}";

    const auto expectedApplicationInfo = ApplicationInfo
    {
        "ExpectedServiceName",
        "ExpectedEnvironment",
        "ExpectedVersion",
        "ExpectedGitRepositoryUrl",
        "ExpectedGitCommitSha"
    };

    applicationStore.SetApplicationInfo(
        runtimeId,
        expectedApplicationInfo.ServiceName,
        expectedApplicationInfo.Environment,
        expectedApplicationInfo.Version);

    {
        auto const& info = applicationStore.GetApplicationInfo(runtimeId);

        ASSERT_EQ(info.Environment, expectedApplicationInfo.Environment);
        ASSERT_EQ(info.ServiceName, expectedApplicationInfo.ServiceName);
        ASSERT_EQ(info.Version, expectedApplicationInfo.Version);
        ASSERT_EQ(info.RepositoryUrl, randomRepoUrl);
        ASSERT_EQ(info.CommitSha, randomCommitSha);
    }

    applicationStore.SetGitMetadata(
        runtimeId,
        expectedApplicationInfo.RepositoryUrl,
        expectedApplicationInfo.CommitSha);

    auto const& info = applicationStore.GetApplicationInfo(runtimeId);

    ASSERT_EQ(info.Environment, expectedApplicationInfo.Environment);
    ASSERT_EQ(info.ServiceName, expectedApplicationInfo.ServiceName);
    ASSERT_EQ(info.Version, expectedApplicationInfo.Version);
    ASSERT_EQ(info.RepositoryUrl, expectedApplicationInfo.RepositoryUrl);
    ASSERT_EQ(info.CommitSha, expectedApplicationInfo.CommitSha);
}
