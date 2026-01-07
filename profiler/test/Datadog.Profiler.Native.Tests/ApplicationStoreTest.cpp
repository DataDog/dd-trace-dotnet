// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationStore.h"
#include "ApplicationInfo.h"

#include "ProfilerMockedInterface.h"
#include "RuntimeInfoHelper.h"

#include "gtest/gtest.h"

using ::testing::ReturnRef;
using ::testing::Return;

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

    auto [ssiManager, mockSsiManager] = CreateSsiManager();
    EXPECT_CALL(mockSsiManager, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    RuntimeInfoHelper helper(6, 0, false);

    ApplicationStore applicationStore(configuration.get(), helper.GetRuntimeInfo());

    const auto& info = applicationStore.GetApplicationInfo("{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}");

    ASSERT_EQ(info.ServiceName, expectedServiceName);
    ASSERT_EQ(info.Version, expectedVersion);
    ASSERT_EQ(info.Environment, expectedEnvironment);
    ASSERT_EQ(info.RepositoryUrl, expectedGitRepository);
    ASSERT_EQ(info.CommitSha, expectedGitCommitSha);
    ASSERT_TRUE(info.ProcessTags.empty());
}

TEST(ApplicationStoreTest, CheckGitMetadataIfSetGitMetadataIsNotCalled)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    std::string expectedGitRepositoryUrl = "ExpectedGitRepositoryUrl";
    std::string expectedGitCommitSha = "ExpectedGitCommitSha";
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(expectedGitRepositoryUrl));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(expectedGitCommitSha));

    auto [ssiManager, mockSsiManager] = CreateSsiManager();
    EXPECT_CALL(mockSsiManager, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    RuntimeInfoHelper helper(6, 0, false);

    ApplicationStore applicationStore(configuration.get(), helper.GetRuntimeInfo());

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
        expectedApplicationInfo.Version,
        std::string());

    auto const& info = applicationStore.GetApplicationInfo(runtimeId);

    ASSERT_EQ(info.Environment, expectedApplicationInfo.Environment);
    ASSERT_EQ(info.ServiceName, expectedApplicationInfo.ServiceName);
    ASSERT_EQ(info.Version, expectedApplicationInfo.Version);
    ASSERT_EQ(info.RepositoryUrl, expectedApplicationInfo.RepositoryUrl);
    ASSERT_EQ(info.CommitSha, expectedApplicationInfo.CommitSha);
    ASSERT_TRUE(info.ProcessTags.empty());
}

TEST(ApplicationStoreTest, MakeSureCallToSetGitMetadataOverrideThePreviousValue)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    std::string randomRepoUrl = "RandomRepoUrl";
    std::string randomCommitSha = "RandomCommitSha";
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(randomRepoUrl));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(randomCommitSha));

    auto [ssiManager, mockSsiManager] = CreateSsiManager();
    EXPECT_CALL(mockSsiManager, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    RuntimeInfoHelper helper(6, 0, false);

    ApplicationStore applicationStore(configuration.get(), helper.GetRuntimeInfo());

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
        expectedApplicationInfo.Version,
        std::string());

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

TEST(ApplicationStoreTest, SetApplicationInfoWithProcessTags)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    std::string randomRepoUrl = "RandomRepoUrl";
    std::string randomCommitSha = "RandomCommitSha";
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(randomRepoUrl));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(randomCommitSha));

    auto [ssiManager, mockSsiManager] = CreateSsiManager();
    EXPECT_CALL(mockSsiManager, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    RuntimeInfoHelper helper(6, 0, false);

    ApplicationStore applicationStore(configuration.get(), helper.GetRuntimeInfo());

    const auto runtimeId = "{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}";
    const std::string expectedProcessTags = "entrypoint.basedir:app,entrypoint.workdir:work";

    // Set application info with process tags (simulates SetConfiguration call with SharedConfig.processTags)
    applicationStore.SetApplicationInfo(
        runtimeId,
        "ExpectedServiceName",
        "ExpectedEnvironment",
        "ExpectedVersion",
        expectedProcessTags);

    auto const& info = applicationStore.GetApplicationInfo(runtimeId);

    ASSERT_EQ(info.ServiceName, "ExpectedServiceName");
    ASSERT_EQ(info.Environment, "ExpectedEnvironment");
    ASSERT_EQ(info.Version, "ExpectedVersion");
    ASSERT_EQ(info.RepositoryUrl, randomRepoUrl);
    ASSERT_EQ(info.CommitSha, randomCommitSha);
    ASSERT_EQ(info.ProcessTags, expectedProcessTags);
}

TEST(ApplicationStoreTest, SetApplicationInfoPreservesExistingProcessTags)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    std::string randomRepoUrl = "RandomRepoUrl";
    std::string randomCommitSha = "RandomCommitSha";
    EXPECT_CALL(mockConfiguration, GetGitRepositoryUrl()).WillRepeatedly(ReturnRef(randomRepoUrl));
    EXPECT_CALL(mockConfiguration, GetGitCommitSha()).WillRepeatedly(ReturnRef(randomCommitSha));

    auto [ssiManager, mockSsiManager] = CreateSsiManager();
    EXPECT_CALL(mockSsiManager, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::Manual));
    RuntimeInfoHelper helper(6, 0, false);

    ApplicationStore applicationStore(configuration.get(), helper.GetRuntimeInfo());

    const auto runtimeId = "{82F18E9B-138D-4202-8D21-7BE1AF82EC8B}";
    const std::string expectedProcessTags = "entrypoint.basedir:app,entrypoint.workdir:work";

    // First call with process tags (simulates SetConfiguration with SharedConfig.processTags)
    applicationStore.SetApplicationInfo(
        runtimeId,
        "ServiceName",
        "Environment",
        "Version",
        expectedProcessTags);

    // Second call with empty process tags (simulates SetApplicationInfoForAppDomain without process tags)
    applicationStore.SetApplicationInfo(
        runtimeId,
        "UpdatedServiceName",
        "UpdatedEnvironment",
        "UpdatedVersion",
        std::string());

    auto const& info = applicationStore.GetApplicationInfo(runtimeId);

    // Service info should be updated
    ASSERT_EQ(info.ServiceName, "UpdatedServiceName");
    ASSERT_EQ(info.Environment, "UpdatedEnvironment");
    ASSERT_EQ(info.Version, "UpdatedVersion");
    
    // But process tags should be preserved (not overwritten by empty string)
    ASSERT_EQ(info.ProcessTags, expectedProcessTags);
}