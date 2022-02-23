// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "LibddprofExporter.h"
#include "OpSysTools.h"

#include "ProfilerMockedInterface.h"

#include <filesystem>

using ::testing::_;
using ::testing::ByMove;
using ::testing::Return;
using ::testing::ReturnRef;
using ::testing::ReturnRefOfCopy;
using ::testing::Throw;

namespace fs = std::filesystem;

TEST(LibddprofExporterTest, CheckProfileIsWrittenToDisk)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    fs::path pprofTempDir = fs::temp_directory_path() / tmpnam(nullptr);
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).Times(1).WillOnce(ReturnRef(pprofTempDir));

    std::string applicationName = "MyApp";
    EXPECT_CALL(mockConfiguration, GetServiceName()).Times(2).WillRepeatedly(ReturnRef(applicationName));

    std::string agentUrl;
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));
    std::string version = "1.0.2";
    EXPECT_CALL(mockConfiguration, GetVersion()).Times(1).WillOnce(ReturnRef(version));
    std::string env = "myenv";
    EXPECT_CALL(mockConfiguration, GetEnvironment()).Times(1).WillOnce(ReturnRef(env));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto exporter = LibddprofExporter(&mockConfiguration);

    auto sample1 = CreateSample({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}},
                                {{"label1", "value1"}, {"label2", "value2"}},
                                42);

    exporter.Add(sample1);

    exporter.Export();

    int nbFile = 0;
    fs::directory_entry pprofFile;
    for (auto const& file : fs::directory_iterator(pprofTempDir))
    {
        pprofFile = file;
        nbFile++;
    }

    EXPECT_EQ(1, nbFile);
    ASSERT_TRUE(pprofFile.is_regular_file());

    std::ostringstream expectedFilePrefix;
    expectedFilePrefix << applicationName << "_" << OpSysTools::GetProcId() << "_";
    EXPECT_THAT(pprofFile.path().filename().string(), ::testing::StartsWith(expectedFilePrefix.str()));
}

TEST(LibddprofExporterTest, MustCreateAgentBasedExporterIfAgentUrlIsSet)
{
    auto [configuration, mockConfiguration] = CreateMockForUniquePtr<IConfiguration, MockConfiguration>();

    std::string agentUrl = "http://host::port";
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(0);

    std::string version = "1.0.2";
    EXPECT_CALL(mockConfiguration, GetVersion()).Times(1).WillOnce(ReturnRef(version));
    std::string env = "myenv";
    EXPECT_CALL(mockConfiguration, GetEnvironment()).Times(1).WillOnce(ReturnRef(env));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    // only used in agentless case
    EXPECT_CALL(mockConfiguration, GetSite()).Times(0);
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(0);

    std::string applicationName = "MyApp";
    EXPECT_CALL(mockConfiguration, GetServiceName()).Times(1).WillOnce(ReturnRef(applicationName));
    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).Times(1).WillOnce(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto exporter = LibddprofExporter(&mockConfiguration);
}

TEST(LibddprofExporterTest, MustCreateAgentBasedExporterIfAgentUrlIsNotSet)
{
    auto [configuration, mockConfiguration] = CreateMockForUniquePtr<IConfiguration, MockConfiguration>();

    std::string agentUrl = "";
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));
    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));

    std::string version = "1.0.2";
    EXPECT_CALL(mockConfiguration, GetVersion()).Times(1).WillOnce(ReturnRef(version));
    std::string env = "myenv";
    EXPECT_CALL(mockConfiguration, GetEnvironment()).Times(1).WillOnce(ReturnRef(env));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    // only used in agentless case
    EXPECT_CALL(mockConfiguration, GetSite()).Times(0);
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(0);

    std::string applicationName = "MyApp";
    EXPECT_CALL(mockConfiguration, GetServiceName()).Times(1).WillOnce(ReturnRef(applicationName));
    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).Times(1).WillOnce(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto exporter = LibddprofExporter(&mockConfiguration);
}

TEST(LibddprofExporterTest, MustCreateAgentLessExporterIfAgentless)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(true));
    std::string site = "test_site";
    EXPECT_CALL(mockConfiguration, GetSite()).Times(1).WillOnce(ReturnRef(site));
    std::string apiKey = "4224";
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(1).WillOnce(ReturnRef(apiKey));
    std::string applicationName = "MyApp";
    EXPECT_CALL(mockConfiguration, GetServiceName()).Times(1).WillOnce(ReturnRef(applicationName));

    // not called when agentless
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(0);

    std::string version = "1.0.2";
    EXPECT_CALL(mockConfiguration, GetVersion()).Times(1).WillOnce(ReturnRef(version));
    std::string env = "myenv";
    EXPECT_CALL(mockConfiguration, GetEnvironment()).Times(1).WillOnce(ReturnRef(env));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).Times(1).WillOnce(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto exporter = LibddprofExporter(&mockConfiguration);
}