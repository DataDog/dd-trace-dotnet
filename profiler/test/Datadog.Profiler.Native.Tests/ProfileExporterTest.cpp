// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "EnabledProfilers.h"
#include "OpSysTools.h"
#include "ProfileExporter.h"

#include "FakeSamples.h"
#include "IMetadataProvider.h"
#include "MetadataProvider.h"
#include "MetricsRegistry.h"
#include "ProfilerMockedInterface.h"
#include "RuntimeInfoHelper.h"
#include "SamplesEnumerator.h"

#include "shared/src/native-src/dd_filesystem.hpp"

using ::testing::_;
using ::testing::Return;
using ::testing::ReturnRef;
using ::testing::Throw;

std::string ComputeExpectedFilePrefix(const std::string& applicationName)
{
    std::ostringstream expectedFilePrefix;
    expectedFilePrefix << applicationName << "_" << OpSysTools::GetProcId() << "_";
    return expectedFilePrefix.str();
}

TEST(ProfileExporterTest, CheckProfileIsWrittenToDisk)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

#ifdef LINUX
    fs::path pprofTempDir = fs::temp_directory_path() / tmpnam(nullptr);
#else
    char tempFilename[L_tmpnam];
    tmpnam_s(tempFilename, sizeof(tempFilename));
    fs::path pprofTempDir = fs::temp_directory_path() / tempFilename;
#endif
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofTempDir));

    std::string agentUrl;
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

#if _WINDOWS
    std::string namedPipeName;
    EXPECT_CALL(mockConfiguration, GetNamedPipeName()).Times(1).WillOnce(ReturnRef(namedPipeName));
#endif

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    std::string firstRid = "MyRid";
    ApplicationInfo firstApplicationInfo{"MyApp", "myenv", "1.0.2"};

    std::string secondRid = "MyRid2";
    ApplicationInfo secondApplicationInfo{"OtherApplication", "myenv", "1.0.2"};

    // Multiple applications
    EXPECT_CALL(applicationStore, GetApplicationInfo(firstRid)).WillRepeatedly(Return(firstApplicationInfo));
    EXPECT_CALL(applicationStore, GetApplicationInfo(secondRid)).WillRepeatedly(Return(secondApplicationInfo));

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});
    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo,
                                    &enabledProfilers, metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);

    // Add samples to only one application
    auto callstack1 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}});
    auto labels1 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}};

    auto sample1 = CreateSample(firstRid,
                                callstack1,
                                labels1,
                                21);

    auto callstack2 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}, {"module", "frame4"}});
    auto labels2 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}, {"label3", "value3"}};
    auto sample2 = CreateSample(firstRid,
                                callstack2,
                                labels2,
                                42);

    auto callstack3 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}});
    auto labels3 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}};
    auto sample3 = CreateSample(firstRid,
                                callstack3,
                                labels3,
                                84);
    exporter.Add(sample1);
    exporter.Add(sample2);
    exporter.Add(sample3);

    exporter.Export();

    std::string expectedPrefix = ComputeExpectedFilePrefix(firstApplicationInfo.ServiceName);

    std::vector<fs::directory_entry> pprofFiles;
    for (auto const& file : fs::directory_iterator(pprofTempDir))
    {
        pprofFiles.push_back(file);
    }

    ASSERT_EQ(pprofFiles.size(), 1);

    auto& file = pprofFiles[0];

    ASSERT_TRUE(file.is_regular_file());

    std::string filename = file.path().filename().string();

    ASSERT_THAT(filename, ::testing::StartsWith(expectedPrefix));

    fs::remove_all(pprofTempDir);
}

// ----------------------------------------------------------------------------------------------
// This test is done in 2 steps:
// - Initialize the exporter internal data structure by add samples for 2 different applications,
//   exporting them to disk, delete the pprof files.
// - Adding a sample to only one application and doing the checks
TEST(ProfileExporterTest, EnsureOnlyProfileWithSamplesIsWrittenToDisk)
{
    // ----------------------------------------------------------------------------------------------
    // First step:
    //
    // Fill the exporter with 2 samples (one per application)
    // Export them to disk
    // Then delete them
    //
    testing::NiceMock<MockConfiguration> mockConfiguration;

#ifdef LINUX
    fs::path pprofTempDir = fs::temp_directory_path() / tmpnam(nullptr);
#else
    char tempFilename[L_tmpnam];
    tmpnam_s(tempFilename, sizeof(tempFilename));
    fs::path pprofTempDir = fs::temp_directory_path() / tempFilename;
#endif
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofTempDir));

    std::string agentUrl;
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

#if _WINDOWS
    std::string namedPipeName;
    EXPECT_CALL(mockConfiguration, GetNamedPipeName()).Times(1).WillOnce(ReturnRef(namedPipeName));
#endif

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    std::string firstRid = "MyRid";
    ApplicationInfo firstApplicationInfo{"MyApp", "myenv", "1.0.2"};

    std::string secondRid = "MyRid2";
    ApplicationInfo secondApplicationInfo{"OtherApplication", "myenv", "1.0.2"};

    EXPECT_CALL(applicationStore, GetApplicationInfo(firstRid)).WillRepeatedly(Return(firstApplicationInfo));
    EXPECT_CALL(applicationStore, GetApplicationInfo(secondRid)).WillRepeatedly(Return(secondApplicationInfo));

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;

    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);

    auto callstack1 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}});
    auto labels1 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}};
    auto sample1 = CreateSample(firstRid,
                                callstack1,
                                labels1,
                                21);

    auto callstack2 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}, {"module", "frame4"}});
    auto labels2 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}, {"label3", "value3"}};
    auto sample2 = CreateSample(secondRid,
                                callstack2,
                                labels2,
                                42);

    exporter.Add(sample1);
    exporter.Add(sample2);

    exporter.Export();

    for (auto const& file : fs::directory_iterator(pprofTempDir))
    {
        fs::remove(file.path());
    }

    // ----------------------------------------------------------------------------------------------
    // Second step:
    // This is where the real test begins

    exporter.Add(sample1);

    exporter.Export();

    std::string expectedPrefix = ComputeExpectedFilePrefix(firstApplicationInfo.ServiceName);

    std::vector<fs::directory_entry> pprofFiles;
    for (auto const& file : fs::directory_iterator(pprofTempDir))
    {
        pprofFiles.push_back(file);
    }

    ASSERT_EQ(pprofFiles.size(), 1);

    auto file = pprofFiles[0];

    ASSERT_TRUE(file.is_regular_file());

    std::string filename = file.path().filename().string();

    ASSERT_THAT(filename, ::testing::StartsWith(expectedPrefix));

    fs::remove_all(pprofTempDir);
}

TEST(ProfileExporterTest, EnsureTwoPprofFilesAreWrittenToDiskForTwoApplications)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

#ifdef LINUX
    fs::path pprofTempDir = fs::temp_directory_path() / tmpnam(nullptr);
#else
    char tempFilename[L_tmpnam];
    tmpnam_s(tempFilename, sizeof(tempFilename));
    fs::path pprofTempDir = fs::temp_directory_path() / tempFilename;
#endif
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofTempDir));

    std::string agentUrl;
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

#if _WINDOWS
    std::string namedPipeName;
    EXPECT_CALL(mockConfiguration, GetNamedPipeName()).Times(1).WillOnce(ReturnRef(namedPipeName));
#endif

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    std::string firstRid = "MyRid";
    ApplicationInfo firstApplicationInfo{"MyApp", "myenv", "1.0.2"};

    std::string secondRid = "MyRid2";
    ApplicationInfo secondApplicationInfo{"OtherApplication", "myenv", "1.0.2"};

    EXPECT_CALL(applicationStore, GetApplicationInfo(firstRid)).WillRepeatedly(Return(firstApplicationInfo));
    EXPECT_CALL(applicationStore, GetApplicationInfo(secondRid)).WillRepeatedly(Return(secondApplicationInfo));

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);

    auto callstack1 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}});
    auto labels1 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}};
    auto sample1 = CreateSample(firstRid,
                                callstack1,
                                labels1,
                                21);

    auto callstack2 = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}, {"module", "frame3"}});
    auto labels2 = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}};
    auto sample2 = CreateSample(secondRid,
                                callstack2,
                                labels2,
                                42);

    exporter.Add(sample1);
    exporter.Add(sample2);

    exporter.Export();

    std::string expectFirstFilePrefix = ComputeExpectedFilePrefix(firstApplicationInfo.ServiceName);
    std::string expectedSecondFilePrefix = ComputeExpectedFilePrefix(secondApplicationInfo.ServiceName);

    std::vector<fs::directory_entry> pprofFiles;
    for (auto const& file : fs::directory_iterator(pprofTempDir))
    {
        pprofFiles.push_back(file);
    }

    ASSERT_EQ(pprofFiles.size(), 2);

    auto firstFile = pprofFiles[0];
    auto secondFile = pprofFiles[1];

    ASSERT_TRUE(firstFile.is_regular_file());
    ASSERT_TRUE(secondFile.is_regular_file());

    std::string firstFilename = firstFile.path().filename().string();
    std::string secondFilename = secondFile.path().filename().string();

    // check if firstFilename starts with expectFirstFilePrefix
    if (firstFilename.rfind(expectFirstFilePrefix) != 0)
    {
        // no, we make sure that firstFilename starts with expectedSecondFilePrefix
        // and secondFilename starts with expectFirstFilePrefix
        ASSERT_THAT(firstFilename, ::testing::StartsWith(expectedSecondFilePrefix));
        ASSERT_THAT(secondFilename, ::testing::StartsWith(expectFirstFilePrefix));
    }
    else
    {
        // firstFilename starts with expectFirstFilePrefix
        ASSERT_THAT(secondFilename, ::testing::StartsWith(expectedSecondFilePrefix));
    }

    fs::remove_all(pprofTempDir);
}

TEST(ProfileExporterTest, MustCreateAgentBasedExporterIfAgentUrlIsSet)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

    std::string agentUrl = "http://host::port";
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(0);

    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    // only used in agentless case
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, GetSite()).Times(0);
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(0);

    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);
}

TEST(ProfileExporterTest, MustCreateAgentBasedExporterIfAgentUrlIsNotSet)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

    std::string agentUrl = "";
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

#if _WINDOWS
    std::string namedPipeName;
    EXPECT_CALL(mockConfiguration, GetNamedPipeName()).Times(1).WillOnce(ReturnRef(namedPipeName));
#endif

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));

    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    // only used in agentless case
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, GetSite()).Times(0);
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(0);

    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);
}

TEST(ProfileExporterTest, MustCreateAgentLessExporterIfAgentless)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(true));
    std::string site = "test_site";
    EXPECT_CALL(mockConfiguration, GetSite()).Times(1).WillOnce(ReturnRef(site));
    std::string apiKey = "4224";
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(1).WillOnce(ReturnRef(apiKey));

    // not called when agentless
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(0);

    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);
}

TEST(ProfileExporterTest, MustCollectSamplesFromProcessProvider)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(true));
    std::string site = "test_site";
    EXPECT_CALL(mockConfiguration, GetSite()).Times(1).WillOnce(ReturnRef(site));
    std::string apiKey = "4224";
    EXPECT_CALL(mockConfiguration, GetApiKey()).Times(1).WillOnce(ReturnRef(apiKey));

    // not called when agentless
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(0);
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(0);

    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));

    fs::path pprofDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofDir));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    MockProcessSamplesProvider processSamplesProvider;
    IMetadataProvider* metadataProvider = nullptr;
    EXPECT_CALL(processSamplesProvider, GetSamples()).Times(1).WillOnce(Return(::testing::ByMove(std::make_unique<FakeSamples>())));
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);

    exporter.RegisterProcessSamplesProvider(static_cast<ISamplesProvider*>(&processSamplesProvider));

    exporter.Export();
}

TEST(ProfileExporterTest, MakeSureNoCrashForReallyLongCallstack)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

    fs::path pprofTempDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofTempDir));

    std::string agentUrl;
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

#if _WINDOWS
    std::string namedPipeName;
    EXPECT_CALL(mockConfiguration, GetNamedPipeName()).Times(1).WillOnce(ReturnRef(namedPipeName));
#endif

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();
    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);
    std::vector<SampleValueType> sampleTypeDefinitions({{"exception", "count"}});

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;  // TODO: could be mocked to test SSI heuristics
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo, &enabledProfilers,
                                    metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);

    std::string runtimeId = "MyRid";
    auto callstack = CreateCallstack(2048);
    auto labels = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}, {"label2", "value2"}};
    auto sample1 = CreateSample(runtimeId, callstack, labels, 42);

    EXPECT_NO_THROW(exporter.Add(sample1));
}

TEST(ProfileExporterTest, CheckNoEnabledProfilers)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(0).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).Times(0).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).Times(0).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsHeapProfilingEnabled()).Times(0).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.empty());
}

TEST(ProfileExporterTest, CheckAllEnabledProfilers)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).Times(1).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, true);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.find("walltime") != std::string::npos);
    ASSERT_TRUE(tag.find("cpu") != std::string::npos);
    ASSERT_TRUE(tag.find("exceptions") != std::string::npos);
    ASSERT_TRUE(tag.find("allocations") != std::string::npos);
    ASSERT_TRUE(tag.find("lock") != std::string::npos);
    ASSERT_TRUE(tag.find("gc") != std::string::npos);
    ASSERT_TRUE(tag.find("heap") != std::string::npos);
}

TEST(ProfileExporterTest, CheckCpuIsEnabled)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(0).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag == "cpu");
}

TEST(ProfileExporterTest, CheckWalltimeIsEnabled)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(0).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag == "walltime");
}

TEST(ProfileExporterTest, CheckExceptionIsEnabled)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(true));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(0).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag == "exceptions");
}

TEST(ProfileExporterTest, CheckAllocationIsEnabledWhenEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(1).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag == "allocations");
}

TEST(ProfileExporterTest, CheckAllocationIsDisabledWhenNoEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(0).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.empty());
}

TEST(ProfileExporterTest, CheckLockContentionIsEnabledWhenEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).Times(1).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag == "lock");
}

TEST(ProfileExporterTest, CheckLockContentionIsDisabledWhenNoEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsContentionProfilingEnabled()).Times(0).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.empty());
}

TEST(ProfileExporterTest, CheckGarbageCollectionIsEnabledWhenEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).Times(1).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag == "gc");
}

TEST(ProfileExporterTest, CheckGarbageCollectionIsDisabledWhenNoEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsGarbageCollectionProfilingEnabled()).Times(0).WillOnce(Return(true));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.empty());
}

TEST(ProfileExporterTest, CheckHeapIsEnabledWhenEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, true);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.find("heap") != std::string::npos);
}

TEST(ProfileExporterTest, CheckHeapIsDisabledWhenNoEvents)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsExceptionProfilingEnabled()).Times(1).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, false, true); // this should never happen but test it anyway

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.empty());
}

TEST(ProfileExporterTest, CheckHeapIsDisabledWhenHeapIsNotEnabled)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(1).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, false);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.find("heap") == std::string::npos);
}

TEST(ProfileExporterTest, CheckAllocationIsEnabledWhenHeapIsEnabled)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    EXPECT_CALL(mockConfiguration, IsWallTimeProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsCpuProfilingEnabled()).Times(1).WillOnce(Return(false));
    EXPECT_CALL(mockConfiguration, IsAllocationProfilingEnabled()).Times(1).WillOnce(Return(false));
    EnabledProfilers enabledProfilers(&mockConfiguration, true, true);

    std::string tag = ProfileExporter::GetEnabledProfilers(&enabledProfilers);

    ASSERT_TRUE(tag.find("allocations") != std::string::npos);
    ASSERT_TRUE(tag.find("heap") != std::string::npos);
}

TEST(ProfileExporterTest, CheckNoCrashWhenProfileCreationFails)
{
    testing::NiceMock<MockConfiguration> mockConfiguration;

    fs::path pprofTempDir;
    EXPECT_CALL(mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(pprofTempDir));

    std::string agentUrl;
    EXPECT_CALL(mockConfiguration, GetAgentUrl()).Times(1).WillOnce(ReturnRef(agentUrl));

#if _WINDOWS
    std::string namedPipeName;
    EXPECT_CALL(mockConfiguration, GetNamedPipeName()).Times(1).WillOnce(ReturnRef(namedPipeName));
#endif

    std::string agentHost = "localhost";
    EXPECT_CALL(mockConfiguration, GetAgentHost()).Times(1).WillOnce(ReturnRef(agentHost));
    int agentPort = 8126;
    EXPECT_CALL(mockConfiguration, GetAgentPort()).Times(1).WillOnce(Return(agentPort));
    std::string host = "localhost";
    EXPECT_CALL(mockConfiguration, GetHostname()).Times(1).WillOnce(ReturnRef(host));
    EXPECT_CALL(mockConfiguration, IsAgentless()).Times(1).WillOnce(Return(false));

    std::vector<std::pair<std::string, std::string>> tags;
    EXPECT_CALL(mockConfiguration, GetUserTags()).Times(1).WillOnce(ReturnRef(tags));

    auto applicationStore = MockApplicationStore();

    std::string runtimeId = "MyRid";
    ApplicationInfo applicationInfo{"MyApp", "myenv", "1.0.2"};
    EXPECT_CALL(applicationStore, GetApplicationInfo(runtimeId)).WillRepeatedly(Return(applicationInfo));

    RuntimeInfoHelper helper(6, 0, false);
    IRuntimeInfo* runtimeInfo = helper.GetRuntimeInfo();
    EnabledProfilers enabledProfilers(&mockConfiguration, false, false);

    // Empty sample type definitions will cause Profile::Create to return nullptr
    std::vector<SampleValueType> sampleTypeDefinitions;

    MetricsRegistry metricsRegistry;
    IAllocationsRecorder* allocRecorder = nullptr;
    IMetadataProvider* metadataProvider = nullptr;
    ISsiManager* ssiManager = nullptr;
    IHeapSnapshotManager* heapSnapshotManager = nullptr;
    auto exporter = ProfileExporter(std::move(sampleTypeDefinitions), &mockConfiguration, &applicationStore, runtimeInfo,
                                    &enabledProfilers, metricsRegistry, metadataProvider, ssiManager, allocRecorder, heapSnapshotManager);

    auto callstack = std::vector<std::pair<std::string, std::string>>({{"module", "frame1"}, {"module", "frame2"}});
    auto labels = std::vector<std::pair<std::string, std::string>>{{"label1", "value1"}};
    auto sample = CreateSample(runtimeId, callstack, labels, 42);

    ASSERT_NO_FATAL_FAILURE(exporter.Add(sample)) << "Adding a sample when profile creation fails must not crash";

    auto exported = exporter.Export();
    ASSERT_FALSE(exported) << "Export must return false when profile creation failed";
}
// ----- GetInfoJson tests -----

struct InfoJsonTestComponents
{
    testing::NiceMock<MockConfiguration> mockConfiguration;
    MockApplicationStore applicationStore;
    std::unique_ptr<RuntimeInfoHelper> runtimeInfoHelper;
    std::unique_ptr<EnabledProfilers> enabledProfilers;
    MetricsRegistry metricsRegistry;
    testing::NiceMock<MockMetadataProvider> mockMetadataProvider;
    testing::NiceMock<MockSsiManager> mockSsiManager;
    MockGcSettingsProvider gcSettingsProvider;

    bool hasSsiManager = false;
    bool hasMetadataProvider = false;

    fs::path pprofDir;
    std::string agentUrl = "http://localhost:8126";
    std::string host = "localhost";
    std::vector<std::pair<std::string, std::string>> tags;
};

std::unique_ptr<InfoJsonTestComponents> CreateInfoJsonTestComponents(bool withSsiManager, bool withMetadataProvider)
{
    auto c = std::make_unique<InfoJsonTestComponents>();
    c->hasSsiManager = withSsiManager;
    c->hasMetadataProvider = withMetadataProvider;

    EXPECT_CALL(c->mockConfiguration, GetProfilesOutputDirectory()).WillRepeatedly(ReturnRef(c->pprofDir));
    EXPECT_CALL(c->mockConfiguration, GetAgentUrl()).WillRepeatedly(ReturnRef(c->agentUrl));
    EXPECT_CALL(c->mockConfiguration, GetHostname()).WillRepeatedly(ReturnRef(c->host));
    EXPECT_CALL(c->mockConfiguration, IsAgentless()).WillRepeatedly(Return(false));
    EXPECT_CALL(c->mockConfiguration, GetUserTags()).WillRepeatedly(ReturnRef(c->tags));

    EXPECT_CALL(c->mockConfiguration, IsWallTimeProfilingEnabled()).WillRepeatedly(Return(true));
    EXPECT_CALL(c->mockConfiguration, IsCpuProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(c->mockConfiguration, IsExceptionProfilingEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(c->mockConfiguration, IsGcThreadsCpuTimeEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(c->mockConfiguration, IsThreadLifetimeEnabled()).WillRepeatedly(Return(false));
    EXPECT_CALL(c->mockConfiguration, GetEnablementStatus()).WillRepeatedly(Return(EnablementStatus::Auto));

    c->runtimeInfoHelper = std::make_unique<RuntimeInfoHelper>(4, 8, true);
    c->enabledProfilers = std::make_unique<EnabledProfilers>(&c->mockConfiguration, false, false);

    if (withSsiManager)
    {
        EXPECT_CALL(c->mockSsiManager, GetDeploymentMode()).WillRepeatedly(Return(DeploymentMode::SingleStepInstrumentation));
    }

    EXPECT_CALL(c->gcSettingsProvider, GetMode()).WillRepeatedly(Return(GCMode::Workstation));

    return c;
}

std::unique_ptr<ProfileExporter> CreateExporterForInfoJsonTest(InfoJsonTestComponents& c)
{
    std::vector<SampleValueType> sampleTypeDefinitions({{"walltime", "nanoseconds"}});
    auto exporter = std::make_unique<ProfileExporter>(
        std::move(sampleTypeDefinitions),
        &c.mockConfiguration,
        &c.applicationStore,
        c.runtimeInfoHelper->GetRuntimeInfo(),
        c.enabledProfilers.get(),
        c.metricsRegistry,
        c.hasMetadataProvider ? &c.mockMetadataProvider : nullptr,
        c.hasSsiManager ? &c.mockSsiManager : nullptr,
        nullptr,
        nullptr);
    exporter->RegisterGcSettingsProvider(&c.gcSettingsProvider);
    return exporter;
}

void AssertValidInfoJsonStructure(const std::string& json)
{
    ASSERT_FALSE(json.empty());
    ASSERT_EQ(json.front(), '{');
    ASSERT_EQ(json.back(), '}');
    ASSERT_EQ(json.find(",}"), std::string::npos) << "Trailing comma found in: " << json;
    ASSERT_EQ(json.find(",,"), std::string::npos) << "Double comma found in: " << json;
    ASSERT_NE(json.find("\"profiler\""), std::string::npos) << "Missing profiler section in: " << json;
    ASSERT_NE(json.find("\"GC Config\""), std::string::npos) << "Missing GC Config section in: " << json;
}

TEST(ProfileExporterTest, InfoJsonIsEmptyWhenNoSsiManager)
{
    auto c = CreateInfoJsonTestComponents(false, false);
    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    ASSERT_TRUE(result.empty());
}

TEST(ProfileExporterTest, InfoJsonIsEmptyWhenMetadataIsEmpty)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t emptyMetadata;
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(emptyMetadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    ASSERT_TRUE(result.empty());
}

TEST(ProfileExporterTest, InfoJsonHasNoTrailingCommaWhenNoEnvVarsSection)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t metadata = {
        {"Unknown Section", {{"key1", "value1"}}}
    };
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(metadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    AssertValidInfoJsonStructure(result);
    ASSERT_EQ(result.find("\"System Properties\""), std::string::npos);
    ASSERT_EQ(result.find("\"System Overrides\""), std::string::npos);
}

TEST(ProfileExporterTest, InfoJsonHasNoTrailingCommaWhenOnlyRuntimeSettings)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t metadata = {
        {MetadataProvider::SectionRuntimeSettings, {{"Start Time", "2026-05-05T17:18:21Z"}}}
    };
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(metadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    AssertValidInfoJsonStructure(result);
    ASSERT_EQ(result.find("\"System Properties\""), std::string::npos);
    ASSERT_EQ(result.find("\"System Overrides\""), std::string::npos);
    ASSERT_EQ(result.find("\"Runtime Settings\""), std::string::npos) << "Runtime Settings should not appear in info JSON";
}

TEST(ProfileExporterTest, InfoJsonContainsSystemPropertiesWhenEnvVarsExist)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t metadata = {
        {MetadataProvider::SectionEnvVars, {{"DD_TRACE_DEBUG", "1"}}}
    };
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(metadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    AssertValidInfoJsonStructure(result);
    ASSERT_NE(result.find("\"System Properties\""), std::string::npos);
    ASSERT_NE(result.find("\"DD_TRACE_DEBUG\""), std::string::npos);
}

TEST(ProfileExporterTest, InfoJsonContainsSystemOverridesWhenOverridesExist)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t metadata = {
        {MetadataProvider::SectionOverrides, {{"DD_PROFILING_ENABLED", "true"}}}
    };
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(metadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    AssertValidInfoJsonStructure(result);
    ASSERT_NE(result.find("\"System Overrides\""), std::string::npos);
    ASSERT_NE(result.find("\"DD_PROFILING_ENABLED\""), std::string::npos);
}

TEST(ProfileExporterTest, InfoJsonContainsBothSectionsWhenBothExist)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t metadata = {
        {MetadataProvider::SectionEnvVars, {{"DD_TRACE_DEBUG", "1"}}},
        {MetadataProvider::SectionOverrides, {{"DD_PROFILING_ENABLED", "true"}}}
    };
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(metadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    AssertValidInfoJsonStructure(result);
    ASSERT_NE(result.find("\"System Properties\""), std::string::npos);
    ASSERT_NE(result.find("\"System Overrides\""), std::string::npos);

    auto propsPos = result.find("\"System Properties\"");
    auto overridesPos = result.find("\"System Overrides\"");
    ASSERT_LT(propsPos, overridesPos) << "System Properties should appear before System Overrides";
}

TEST(ProfileExporterTest, InfoJsonHandlesEmptyKeyValueList)
{
    auto c = CreateInfoJsonTestComponents(true, true);

    IMetadataProvider::metadata_t metadata = {
        {MetadataProvider::SectionEnvVars, {}}
    };
    EXPECT_CALL(c->mockMetadataProvider, Get()).WillRepeatedly(ReturnRef(metadata));

    auto exporter = CreateExporterForInfoJsonTest(*c);

    std::string runtimeId = "test-rid";
    auto result = exporter->GetInfoJson(runtimeId);

    AssertValidInfoJsonStructure(result);
    ASSERT_NE(result.find("\"System Properties\": {}"), std::string::npos)
        << "Expected empty System Properties object in: " << result;
}
