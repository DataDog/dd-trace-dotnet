// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "Configuration.h"
#include "EnvironmentHelper.h"
#include "EnvironmentVariables.h"
#include "OpSysTools.h"

#include "shared/src/native-src/string.h"

using namespace std::literals::chrono_literals;

extern void unsetenv(const shared::WSTRING& name);

TEST(ConfigurationTest, CheckIfDebugLogIsNotEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::DebugLogEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugLogEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugLogEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugLogEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenInDev)
{
    unsetenv(EnvironmentVariables::DebugLogEnabled);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));

    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NativeFramesEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsNativeFramesEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NativeFramesEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NativeFramesEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::NativeFramesEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::OperationalMetricsEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsOperationalMetricsEnabled());
}

TEST(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::OperationalMetricsEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::OperationalMetricsEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST(ConfigurationTest, CheckIfOperationalMetricsIsNotEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::OperationalMetricsEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST(ConfigurationTest, CheckDefaultLogDirectoryWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::LogDirectory);
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef _WINDOWS
        WStr("C:\\ProgramData\\Datadog .NET Tracer\\logs");
#else
        WStr("/var/log/datadog/dotnet");
#endif
    ASSERT_EQ(expectedValue, configuration.GetLogDirectory());
}

TEST(ConfigurationTest, CheckLogDirectoryWhenVariableIsSet)
{
    auto expectedValue = fs::path(WStr("MyFolder/WhereIWantIt/ToBe"));
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::LogDirectory, shared::ToWSTRING(expectedValue.string()));
    auto configuration = Configuration{};
    ASSERT_EQ(expectedValue, configuration.GetLogDirectory());
}

TEST(ConfigurationTest, CheckNoDefaultPprofDirectoryWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ProfilesOutputDir);
    auto configuration = Configuration{};
    auto expectedValue = shared::WSTRING();
    ASSERT_EQ(expectedValue, configuration.GetProfilesOutputDirectory());
}

TEST(ConfigurationTest, CheckProfileDirectoryWhenVariableIsSet)
{
    auto expectedValue = fs::path(WStr("MyFolder/WhereIWantIt/ToBe"));
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilesOutputDir, shared::ToWSTRING(expectedValue.string()));
    auto configuration = Configuration{};
    ASSERT_EQ(expectedValue, configuration.GetProfilesOutputDirectory());
}

TEST(ConfigurationTest, CheckDefaultUploadIntervalInDevMode)
{
    unsetenv(EnvironmentVariables::UploadInterval);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_EQ(20s, configuration.GetUploadInterval());
}

TEST(ConfigurationTest, CheckDefaultUploadIntervalInNonDevMode)
{
    unsetenv(EnvironmentVariables::UploadInterval);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_EQ(60s, configuration.GetUploadInterval());
}

TEST(ConfigurationTest, CheckUploadIntervalWhenVariableIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::UploadInterval, WStr("200"));
    auto configuration = Configuration{};
    ASSERT_EQ(200s, configuration.GetUploadInterval());
}

TEST(ConfigurationTest, CheckDefaultSiteInDevMode)
{
    unsetenv(EnvironmentVariables::Site);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_EQ("datad0g.com", configuration.GetSite());
}

TEST(ConfigurationTest, CheckDefaultSiteInNonDevMode)
{
    unsetenv(EnvironmentVariables::Site);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_EQ("datadoghq.com", configuration.GetSite());
}

TEST(ConfigurationTest, CheckSiteWhenVariableIsSet)
{
    auto expectedValue = WStr("MySite");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Site, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetSite());
}

TEST(ConfigurationTest, CheckVersionIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Version);
    auto configuration = Configuration{};
    ASSERT_EQ("Unspecified-Version", configuration.GetVersion());
}

TEST(ConfigurationTest, CheckVersionWhenVariableIsSet)
{
    auto expectedValue = WStr("MyVersion");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Version, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetVersion());
}

TEST(ConfigurationTest, CheckEnvironmentIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Environment);
    auto configuration = Configuration{};
    ASSERT_EQ("Unspecified-Environment", configuration.GetEnvironment());
}

TEST(ConfigurationTest, CheckEnvrionmentWhenVariableIsSet)
{
    auto expectedValue = WStr("MyEnv");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Environment, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetEnvironment());
}

TEST(ConfigurationTest, CheckHostnameIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Hostname);
    auto configuration = Configuration{};
    ASSERT_EQ(OpSysTools::GetHostname(), configuration.GetHostname());
}

TEST(ConfigurationTest, CheckHostnameWhenVariableIsSet)
{
    auto expectedValue = WStr("Myhost");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Environment, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetEnvironment());
}

TEST(ConfigurationTest, CheckAgentUrlIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::AgentUrl);
    auto configuration = Configuration{};
    ASSERT_EQ("", configuration.GetAgentUrl());
}

TEST(ConfigurationTest, CheckAgentUrlWhenVariableIsSet)
{
    auto expectedValue = WStr("MyAgentUrl");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AgentUrl, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetAgentUrl());
}

TEST(ConfigurationTest, CheckAgentHostIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::AgentHost);
    auto configuration = Configuration{};
    ASSERT_EQ("localhost", configuration.GetAgentHost());
}

TEST(ConfigurationTest, CheckAgentHostWhenVariableIsSet)
{
    auto expectedValue = WStr("MyAgenthost");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AgentHost, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetAgentHost());
}

TEST(ConfigurationTest, CheckAgentPortIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::AgentPort);
    auto configuration = Configuration{};
    ASSERT_EQ(8126, configuration.GetAgentPort());
}

TEST(ConfigurationTest, CheckAgentPortWhenVariableIsSet)
{
    auto expectedValue = WStr("4242");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AgentPort, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(4242, configuration.GetAgentPort());
}

TEST(ConfigurationTest, CheckApiKeyIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ApiKey);
    auto configuration = Configuration{};
    ASSERT_EQ("", configuration.GetApiKey());
}

TEST(ConfigurationTest, CheckApiKeyWhenVariableIsSet)
{
    auto expectedValue = WStr("4242XXX");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ApiKey, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetApiKey());
}

TEST(ConfigurationTest, CheckApplicationNameIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ServiceName);
    auto configuration = Configuration{};
    ASSERT_EQ(OpSysTools::GetProcessName(), configuration.GetServiceName());
}

TEST(ConfigurationTest, CheckApplicationNameWhenVariableIsSet)
{
    auto expectedValue = WStr("MyApplication");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ServiceName, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetServiceName());
}

TEST(ConfigurationTest, CheckUserTagsWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Tags);
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::IsEmpty());
}

TEST(ConfigurationTest, CheckUserTagsWhenVariableIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Tags, WStr("foo:bar,lab1:val1"));
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::ContainerEq(tags{{"foo", "bar"}, {"lab1", "val1"}}));
}

TEST(ConfigurationTest, CheckUserTagsWhenVariableIsSetWithIncompleteTag)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Tags, WStr("foo:bar,foobar:barbar,lab1:"));
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::ContainerEq(tags{{"foo", "bar"}, {"foobar", "barbar"}, {"lab1", ""}}));
}

TEST(ConfigurationTest, CheckDefaultMinimumCoresThresholdWhenNoValue)
{
    auto configuration = Configuration{};
    ASSERT_EQ(configuration.MinimumCores(), 1.0);
}

TEST(ConfigurationTest, CheckDefaultMinimumCoresThresholdWhenInvalidValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CoreMinimumOverride, WStr("invalid"));
    auto configuration = Configuration{};
    ASSERT_EQ(configuration.MinimumCores(), 1.0);
}

TEST(ConfigurationTest, CheckMinimumCoresThresholdWhenVariableIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CoreMinimumOverride, WStr("0.5"));
    auto configuration = Configuration{};
    ASSERT_EQ(configuration.MinimumCores(), 0.5);
}

TEST(ConfigurationTest, CheckContentionProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckContentionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::LockContentionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckContentionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::LockContentionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckDeprecatedContentionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckDeprecatedContentionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckLockProfilingOverrideContentionEnvVarIfSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar1(EnvironmentVariables::LockContentionProfilingEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckLockProfilingOverrideContentionEnvVarIfSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar1(EnvironmentVariables::LockContentionProfilingEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckContentionSampleLimitIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ContentionSampleLimit, WStr("123"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.ContentionSampleLimit(), 123);
}

TEST(ConfigurationTest, CheckContentionDurationThresholdIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ContentionDurationThreshold, WStr("123"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.ContentionDurationThreshold(), 123);
}

TEST(ConfigurationTest, CheckCpuProfilingIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCpuProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckCpuProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCpuProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckCpuProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCpuProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckCpuWallTimeSamplingRateIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuWallTimeSamplingRate, WStr("123"));
    auto configuration = Configuration{};
    auto rate = configuration.CpuWallTimeSamplingRate();
    ASSERT_THAT(rate, std::chrono::nanoseconds(123000000));
}

TEST(ConfigurationTest, CheckCpuWallTimeSamplingRateIfNotSet)
{
    auto configuration = Configuration{};
    auto rate = configuration.CpuWallTimeSamplingRate();
    auto count = rate.count();
    ASSERT_THAT(rate, std::chrono::nanoseconds(9000000));
}

TEST(ConfigurationTest, CheckCpuWallTimeSamplingRateIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuWallTimeSamplingRate, WStr("1"));
    auto configuration = Configuration{};
    auto rate = configuration.CpuWallTimeSamplingRate();
    auto count = rate.count();
    ASSERT_THAT(rate, std::chrono::nanoseconds(5000000));
}

TEST(ConfigurationTest, CheckAllocationProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsAllocationProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckAllocationProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AllocationProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsAllocationProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckAllocationProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AllocationProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsAllocationProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckAllocationSampleLimitIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AllocationSampleLimit, WStr("123"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.AllocationSampleLimit(), 123);
}

TEST(ConfigurationTest, CheckNamedPipeIsDisabledByDefault)
{
    auto configuration = Configuration{};
    EXPECT_EQ(configuration.GetNamedPipeName(), std::string());
}

TEST(ConfigurationTest, CheckNamedPipePathWhenProvided)
{
    std::string expectedPath = R"(\\.\mypipe\comeon)";
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NamedPipeName, shared::ToWSTRING(expectedPath));
    auto configuration = Configuration{};
    EXPECT_EQ(configuration.GetNamedPipeName(), expectedPath);
}

TEST(ConfigurationTest, CheckTimestampAsLabelIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsTimestampsAsLabelEnabled(), true);
}

TEST(ConfigurationTest, CheckTimestampAsLabelIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::TimestampsAsLabelEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsTimestampsAsLabelEnabled(), true);
}

TEST(ConfigurationTest, CheckTimestampAsLabelIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::TimestampsAsLabelEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsTimestampsAsLabelEnabled(), false);
}

TEST(ConfigurationTest, CheckWallTimeThreadsThresholdIfNoValue)
{
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 5);
}

TEST(ConfigurationTest, CheckWallTimeThreadsThresholdIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WalltimeThreadsThreshold, WStr("1"));
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 5);
}

TEST(ConfigurationTest, CheckWallTimeThreadsThresholdIfTooLargeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WalltimeThreadsThreshold, WStr("5000"));
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 64);
}

TEST(ConfigurationTest, CheckWallTimeThreadsThresholdIfCorrectValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WalltimeThreadsThreshold, WStr("16"));
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 16);
}

TEST(ConfigurationTest, CheckCpuThreadsThresholdIfNoValue)
{
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 64);
}

TEST(ConfigurationTest, CheckCpuThreadsThresholdIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuTimeThreadsThreshold, WStr("1"));
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 5);
}

TEST(ConfigurationTest, CheckCpuThreadsThresholdIfTooLargeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuTimeThreadsThreshold, WStr("5000"));
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 128);
}

TEST(ConfigurationTest, CheckCpuThreadsThresholdIfCorrectValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuTimeThreadsThreshold, WStr("16"));
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 16);
}

TEST(ConfigurationTest, CheckGarbageCollectionProfilingIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGarbageCollectionProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckGarbageCollectionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GCProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGarbageCollectionProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckGarbageCollectionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GCProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGarbageCollectionProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckHeapProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckHeapProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapProfilingEnabled(), true);
}

TEST(ConfigurationTest, CheckHeapProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapProfilingEnabled(), false);
}

TEST(ConfigurationTest, CheckBacktrace2IsUsedByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.UseBacktrace2(), true);
}

TEST(ConfigurationTest, CheckBacktrace2IsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::UseBacktrace2, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.UseBacktrace2(), false);
}

TEST(ConfigurationTest, CheckBacktrace2IsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::UseBacktrace2, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.UseBacktrace2(), true);
}

TEST(ConfigurationTest, CheckDebugInfoIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsDebugInfoEnabled(), false);
}

TEST(ConfigurationTest, CheckDebugInfoIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugInfoEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsDebugInfoEnabled(), true);
}

TEST(ConfigurationTest, CheckDebugInfoIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugInfoEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsDebugInfoEnabled(), false);
}