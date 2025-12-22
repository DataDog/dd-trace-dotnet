// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "Configuration.h"
#include "EnvironmentHelper.h"
#include "EnvironmentVariables.h"
#include "OpSysTools.h"

#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

using namespace std::literals::chrono_literals;

extern void unsetenv(const shared::WSTRING& name);
extern void setenv(const shared::WSTRING& name, const shared::WSTRING& value);

class ConfigurationTest : public testing::Test {
private:
    std::vector<std::tuple<shared::WSTRING, shared::WSTRING>> _variables;
protected:
    ConfigurationTest()
    {
        shared::WSTRING variables_to_save[]{
            WStr("DD_INJECTION_ENABLED"),
            WStr("DD_INJECT_FORCE"),
            WStr("DD_TELEMETRY_FORWARDER_PATH"),
        };

        for (auto&& variable_to_save : variables_to_save)
        {
            if (shared::EnvironmentExist(variable_to_save))
            {
                _variables.push_back(make_tuple(variable_to_save, shared::GetEnvironmentValue(variable_to_save)));
                unsetenv(variable_to_save);
            }
        }
    }

    ~ConfigurationTest() override
    {
        for (auto&& variable_to_restore : _variables)
        {
            setenv(std::get<0>(variable_to_restore), std::get<1>(variable_to_restore));
        }
    }
};

TEST_F(ConfigurationTest, CheckIfDebugLogIsNotEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::DebugLogEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST_F(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugLogEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsDebugLogEnabled());
}

TEST_F(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugLogEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST_F(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugLogEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST_F(ConfigurationTest, CheckIfDebugLogIsEnabledWhenInDev)
{
    unsetenv(EnvironmentVariables::DebugLogEnabled);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));

    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsDebugLogEnabled());
}

TEST_F(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NativeFramesEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsNativeFramesEnabled());
}

TEST_F(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NativeFramesEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST_F(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NativeFramesEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST_F(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::NativeFramesEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST_F(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::OperationalMetricsEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsOperationalMetricsEnabled());
}

TEST_F(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::OperationalMetricsEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST_F(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::OperationalMetricsEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST_F(ConfigurationTest, CheckIfOperationalMetricsIsNotEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::OperationalMetricsEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST_F(ConfigurationTest, CheckDefaultLogDirectoryWhenVariableIsNotSet)
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

TEST_F(ConfigurationTest, CheckLogDirectoryWhenVariableIsSet)
{
    auto expectedValue = fs::path(WStr("MyFolder/WhereIWantIt/ToBe"));
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::LogDirectory, shared::ToWSTRING(expectedValue.string()));
    auto configuration = Configuration{};
    ASSERT_EQ(expectedValue, configuration.GetLogDirectory());
}

TEST_F(ConfigurationTest, CheckNoDefaultPprofDirectoryWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ProfilesOutputDir);
    auto configuration = Configuration{};
    auto expectedValue = shared::WSTRING();
    ASSERT_EQ(expectedValue, configuration.GetProfilesOutputDirectory());
}

TEST_F(ConfigurationTest, CheckProfileDirectoryWhenVariableIsSet)
{
    auto expectedValue = fs::path(WStr("MyFolder/WhereIWantIt/ToBe"));
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilesOutputDir, shared::ToWSTRING(expectedValue.string()));
    auto configuration = Configuration{};
    ASSERT_EQ(expectedValue, configuration.GetProfilesOutputDirectory());
}

TEST_F(ConfigurationTest, CheckDefaultUploadIntervalInDevMode)
{
    unsetenv(EnvironmentVariables::UploadInterval);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_EQ(20s, configuration.GetUploadInterval());
}

TEST_F(ConfigurationTest, CheckDefaultUploadIntervalInNonDevMode)
{
    unsetenv(EnvironmentVariables::UploadInterval);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_EQ(60s, configuration.GetUploadInterval());
}

TEST_F(ConfigurationTest, CheckUploadIntervalWhenVariableIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::UploadInterval, WStr("200"));
    auto configuration = Configuration{};
    ASSERT_EQ(200s, configuration.GetUploadInterval());
}

TEST_F(ConfigurationTest, CheckDefaultSiteInDevMode)
{
    unsetenv(EnvironmentVariables::Site);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_EQ("datad0g.com", configuration.GetSite());
}

TEST_F(ConfigurationTest, CheckDefaultSiteInNonDevMode)
{
    unsetenv(EnvironmentVariables::Site);
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DevelopmentConfiguration, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_EQ("datadoghq.com", configuration.GetSite());
}

TEST_F(ConfigurationTest, CheckSiteWhenVariableIsSet)
{
    auto expectedValue = WStr("MySite");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Site, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetSite());
}

TEST_F(ConfigurationTest, CheckVersionIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Version);
    auto configuration = Configuration{};
    ASSERT_EQ("Unspecified-Version", configuration.GetVersion());
}

TEST_F(ConfigurationTest, CheckVersionWhenVariableIsSet)
{
    auto expectedValue = WStr("MyVersion");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Version, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetVersion());
}

TEST_F(ConfigurationTest, CheckEnvironmentIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Environment);
    auto configuration = Configuration{};
    ASSERT_EQ("Unspecified-Environment", configuration.GetEnvironment());
}

TEST_F(ConfigurationTest, CheckEnvrionmentWhenVariableIsSet)
{
    auto expectedValue = WStr("MyEnv");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Environment, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetEnvironment());
}

TEST_F(ConfigurationTest, CheckHostnameIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Hostname);
    auto configuration = Configuration{};
    ASSERT_EQ(OpSysTools::GetHostname(), configuration.GetHostname());
}

TEST_F(ConfigurationTest, CheckHostnameWhenVariableIsSet)
{
    auto expectedValue = WStr("Myhost");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Environment, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetEnvironment());
}

TEST_F(ConfigurationTest, CheckAgentUrlIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::AgentUrl);
    auto configuration = Configuration{};
    ASSERT_EQ("", configuration.GetAgentUrl());
}

TEST_F(ConfigurationTest, CheckAgentUrlWhenVariableIsSet)
{
    auto expectedValue = WStr("MyAgentUrl");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AgentUrl, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetAgentUrl());
}

TEST_F(ConfigurationTest, CheckAgentHostIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::AgentHost);
    auto configuration = Configuration{};
    ASSERT_EQ("localhost", configuration.GetAgentHost());
}

TEST_F(ConfigurationTest, CheckAgentHostWhenVariableIsSet)
{
    auto expectedValue = WStr("MyAgenthost");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AgentHost, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetAgentHost());
}

TEST_F(ConfigurationTest, CheckAgentPortIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::AgentPort);
    auto configuration = Configuration{};
    ASSERT_EQ(8126, configuration.GetAgentPort());
}

TEST_F(ConfigurationTest, CheckAgentPortWhenVariableIsSet)
{
    auto expectedValue = WStr("4242");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AgentPort, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(4242, configuration.GetAgentPort());
}

TEST_F(ConfigurationTest, CheckApiKeyIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ApiKey);
    auto configuration = Configuration{};
    ASSERT_EQ("", configuration.GetApiKey());
}

TEST_F(ConfigurationTest, CheckApiKeyWhenVariableIsSet)
{
    auto expectedValue = WStr("4242XXX");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ApiKey, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetApiKey());
}

TEST_F(ConfigurationTest, CheckApplicationNameIfVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ServiceName);
    auto configuration = Configuration{};
    ASSERT_EQ(OpSysTools::GetProcessName(), configuration.GetServiceName());
}

TEST_F(ConfigurationTest, CheckApplicationNameWhenVariableIsSet)
{
    auto expectedValue = WStr("MyApplication");
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ServiceName, expectedValue);
    auto configuration = Configuration{};
    ASSERT_EQ(shared::ToString(expectedValue), configuration.GetServiceName());
}

TEST_F(ConfigurationTest, CheckUserTagsWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::Tags);
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::IsEmpty());
}

TEST_F(ConfigurationTest, CheckUserTagsWhenVariableIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Tags, WStr("foo:bar,lab1:val1"));
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::ContainerEq(tags{{"foo", "bar"}, {"lab1", "val1"}}));
}

TEST_F(ConfigurationTest, CheckUserTagsWhenVariableIsSetWithIncompleteTag)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::Tags, WStr("foo:bar,foobar:barbar,lab1:"));
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::ContainerEq(tags{{"foo", "bar"}, {"foobar", "barbar"}, {"lab1", ""}}));
}

TEST_F(ConfigurationTest, CheckDefaultMinimumCoresThresholdWhenNoValue)
{
    auto configuration = Configuration{};
    ASSERT_EQ(configuration.MinimumCores(), 1.0);
}

TEST_F(ConfigurationTest, CheckDefaultMinimumCoresThresholdWhenInvalidValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CoreMinimumOverride, WStr("invalid"));
    auto configuration = Configuration{};
    ASSERT_EQ(configuration.MinimumCores(), 1.0);
}

TEST_F(ConfigurationTest, CheckMinimumCoresThresholdWhenVariableIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CoreMinimumOverride, WStr("0.5"));
    auto configuration = Configuration{};
    ASSERT_EQ(configuration.MinimumCores(), 0.5);
}

TEST_F(ConfigurationTest, CheckExceptionProfilingIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsExceptionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckExceptionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ExceptionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsExceptionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckExceptionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ExceptionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsExceptionProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckContentionProfilingIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckContentionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::LockContentionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckContentionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::LockContentionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckDeprecatedContentionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckDeprecatedContentionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckLockProfilingOverrideContentionEnvVarIfSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar1(EnvironmentVariables::LockContentionProfilingEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckLockProfilingOverrideContentionEnvVarIfSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar1(EnvironmentVariables::LockContentionProfilingEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::DeprecatedContentionProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsContentionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckContentionSampleLimitIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ContentionSampleLimit, WStr("123"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.ContentionSampleLimit(), 123);
}

TEST_F(ConfigurationTest, CheckContentionDurationThresholdIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ContentionDurationThreshold, WStr("123"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.ContentionDurationThreshold(), 123);
}

TEST_F(ConfigurationTest, CheckCpuProfilingIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCpuProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckCpuProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCpuProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckCpuProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCpuProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckCpuWallTimeSamplingRateIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuWallTimeSamplingRate, WStr("123"));
    auto configuration = Configuration{};
    auto rate = configuration.CpuWallTimeSamplingRate();
    ASSERT_THAT(rate, std::chrono::nanoseconds(123000000));
}

TEST_F(ConfigurationTest, CheckCpuWallTimeSamplingRateIfNotSet)
{
    auto configuration = Configuration{};
    auto rate = configuration.CpuWallTimeSamplingRate();
    auto count = rate.count();
    ASSERT_THAT(rate, std::chrono::nanoseconds(9000000));
}

TEST_F(ConfigurationTest, CheckCpuWallTimeSamplingRateIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuWallTimeSamplingRate, WStr("1"));
    auto configuration = Configuration{};
    auto rate = configuration.CpuWallTimeSamplingRate();
    auto count = rate.count();
    ASSERT_THAT(rate, std::chrono::nanoseconds(5000000));
}

TEST_F(ConfigurationTest, CheckAllocationProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsAllocationProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckAllocationProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AllocationProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsAllocationProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckAllocationProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AllocationProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsAllocationProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckAllocationSampleLimitIfEnvVarSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::AllocationSampleLimit, WStr("123"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.AllocationSampleLimit(), 123);
}

TEST_F(ConfigurationTest, CheckNamedPipeIsDisabledByDefault)
{
    auto configuration = Configuration{};
    EXPECT_EQ(configuration.GetNamedPipeName(), std::string());
}

TEST_F(ConfigurationTest, CheckNamedPipePathWhenProvided)
{
    std::string expectedPath = R"(\\.\mypipe\comeon)";
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::NamedPipeName, shared::ToWSTRING(expectedPath));
    auto configuration = Configuration{};
    EXPECT_EQ(configuration.GetNamedPipeName(), expectedPath);
}

TEST_F(ConfigurationTest, CheckTimestampAsLabelIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsTimestampsAsLabelEnabled(), true);
}

TEST_F(ConfigurationTest, CheckTimestampAsLabelIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::TimestampsAsLabelEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsTimestampsAsLabelEnabled(), true);
}

TEST_F(ConfigurationTest, CheckTimestampAsLabelIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::TimestampsAsLabelEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsTimestampsAsLabelEnabled(), false);
}

TEST_F(ConfigurationTest, CheckWallTimeThreadsThresholdIfNoValue)
{
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 5);
}

TEST_F(ConfigurationTest, CheckWallTimeThreadsThresholdIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WalltimeThreadsThreshold, WStr("1"));
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 5);
}

TEST_F(ConfigurationTest, CheckWallTimeThreadsThresholdIfTooLargeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WalltimeThreadsThreshold, WStr("5000"));
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 64);
}

TEST_F(ConfigurationTest, CheckWallTimeThreadsThresholdIfCorrectValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WalltimeThreadsThreshold, WStr("16"));
    auto configuration = Configuration{};
    auto threshold = configuration.WalltimeThreadsThreshold();
    ASSERT_THAT(threshold, 16);
}

TEST_F(ConfigurationTest, CheckCpuThreadsThresholdIfNoValue)
{
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 64);
}

TEST_F(ConfigurationTest, CheckCpuThreadsThresholdIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuTimeThreadsThreshold, WStr("1"));
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 5);
}

TEST_F(ConfigurationTest, CheckCpuThreadsThresholdIfTooLargeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuTimeThreadsThreshold, WStr("5000"));
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 128);
}

TEST_F(ConfigurationTest, CheckCpuThreadsThresholdIfCorrectValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuTimeThreadsThreshold, WStr("16"));
    auto configuration = Configuration{};
    auto threshold = configuration.CpuThreadsThreshold();
    ASSERT_THAT(threshold, 16);
}

TEST_F(ConfigurationTest, CheckGarbageCollectionProfilingIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGarbageCollectionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckGarbageCollectionProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GCProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGarbageCollectionProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckGarbageCollectionProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GCProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGarbageCollectionProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckHeapProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckHeapProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckHeapProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckDebugInfoIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsDebugInfoEnabled(), false);
}

TEST_F(ConfigurationTest, CheckDebugInfoIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugInfoEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsDebugInfoEnabled(), true);
}

TEST_F(ConfigurationTest, CheckDebugInfoIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::DebugInfoEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsDebugInfoEnabled(), false);
}

TEST_F(ConfigurationTest, CheckGcThreadsCpuTimeEnabledTakesOverInternal)
{
    EnvironmentHelper::EnvironmentVariable ev1(EnvironmentVariables::GcThreadsCpuTimeEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ev2(EnvironmentVariables::GcThreadsCpuTimeInternalEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsGcThreadsCpuTimeEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckGcThreadsCpuTimeDisabledTakesOverInternal)
{
    EnvironmentHelper::EnvironmentVariable ev1(EnvironmentVariables::GcThreadsCpuTimeEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ev2(EnvironmentVariables::GcThreadsCpuTimeInternalEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsGcThreadsCpuTimeEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckInternalGcThreadsCpuTimeProfilingIsTakenIntoAccount)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GcThreadsCpuTimeInternalEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsGcThreadsCpuTimeEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckGcThreadsCpuTimeIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGcThreadsCpuTimeEnabled(), true);
}

TEST_F(ConfigurationTest, CheckGcThreadsCpuTimeIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GcThreadsCpuTimeEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGcThreadsCpuTimeEnabled(), true);
}

TEST_F(ConfigurationTest, CheckGcThreadsCpuTimeIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GcThreadsCpuTimeEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsGcThreadsCpuTimeEnabled(), false);
}

TEST_F(ConfigurationTest, CheckThreadLifetimeEnabledTakesOverInternal)
{
    EnvironmentHelper::EnvironmentVariable ev1(EnvironmentVariables::ThreadLifetimeEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ev2(EnvironmentVariables::ThreadLifetimeInternalEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsThreadLifetimeEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckThreadLifetimeDisabledTakesOverInternal)
{
    EnvironmentHelper::EnvironmentVariable ev1(EnvironmentVariables::ThreadLifetimeEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ev2(EnvironmentVariables::ThreadLifetimeInternalEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsThreadLifetimeEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckInternalThreadLifetimeProfilingIsTakenIntoAccount)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ThreadLifetimeInternalEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsThreadLifetimeEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckThreadLifetimeIsEnabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsThreadLifetimeEnabled(), true);
}

TEST_F(ConfigurationTest, CheckThreadLifetimeIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ThreadLifetimeEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsThreadLifetimeEnabled(), true);
}

TEST_F(ConfigurationTest, CheckThreadLifetimeIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ThreadLifetimeEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsThreadLifetimeEnabled(), false);
}

TEST_F(ConfigurationTest, CheckGitMetadataIfNotSet)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetGitRepositoryUrl(), "");
    ASSERT_THAT(configuration.GetGitCommitSha(), "");
}

TEST_F(ConfigurationTest, CheckGitMetadataIfSet)
{
    std::string expectedRepoUrl = "http://dotnet";
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::GitRepositoryUrl, shared::ToWSTRING(expectedRepoUrl));
    std::string expectedCommitSha = "42";
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::GitCommitSha, shared::ToWSTRING(expectedCommitSha));

    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetGitRepositoryUrl(), expectedRepoUrl);
    ASSERT_THAT(configuration.GetGitCommitSha(), expectedCommitSha);
}

TEST_F(ConfigurationTest, CheckSystemCallsShieldIsEnabledByDefault)
{
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef LINUX
        true;
#else
        false;
#endif
    ASSERT_THAT(configuration.IsSystemCallsShieldEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSystemCallsShieldIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SystemCallsShieldEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef LINUX
        true;
#else
        false; // even other platform it's disabled
#endif
    ASSERT_THAT(configuration.IsSystemCallsShieldEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSystemCallsShieldIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SystemCallsShieldEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsSystemCallsShieldEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckCIVisibilityEnabledDefaultValueIfNotSet)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCIVisibilityEnabled(), false);
}

TEST_F(ConfigurationTest, CheckCIVisibilityEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CIVisibilityEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCIVisibilityEnabled(), true);
}

TEST_F(ConfigurationTest, CheckCIVisibilityEnabledIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CIVisibilityEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsCIVisibilityEnabled(), false);
}

TEST_F(ConfigurationTest, CheckCIVisibilitySpanIdDefaultValueIfNotSet)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCIVisibilitySpanId(), 0ull);
}

TEST_F(ConfigurationTest, CheckCIVisibilitySpanIdValueIfSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CIVisibilityEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::InternalCIVisibilitySpanId, WStr("12345678909"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCIVisibilitySpanId(), 12345678909ull);
}

TEST_F(ConfigurationTest, CheckCIVisibilitySpanIdValueIfSetTo0)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CIVisibilityEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::InternalCIVisibilitySpanId, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCIVisibilitySpanId(), 0ull);
}

TEST_F(ConfigurationTest, CheckEtwIsEnabledByDefault)
{
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef LINUX
        false;
#else
        true;
#endif
    ASSERT_THAT(configuration.IsEtwEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckEtwIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::EtwEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef LINUX
        false;
#else
        true;
#endif
    ASSERT_THAT(configuration.IsEtwEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckEtwIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::EtwEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsEtwEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckEtwReplayEndpointIsEmptyIfEnvVarNotSet)
{
    auto configuration = Configuration{};
    std::string expectedValue = "";
    ASSERT_THAT(configuration.GetEtwReplayEndpoint(), expectedValue);
}

// Windows named pipe paths are not supported on Linux
#ifndef LINUX
TEST_F(ConfigurationTest, CheckEtwReplayEndpointIfEnvVarIsSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::EtwReplayEndpoint, WStr("\\\\.\\pipe\\DD_ETW_FOR_TEST"));
    auto configuration = Configuration{};
    std::string expectedValue = "\\\\.\\pipe\\DD_ETW_FOR_TEST";
    ASSERT_THAT(configuration.GetEtwReplayEndpoint(), expectedValue);
}
#endif

TEST_F(ConfigurationTest, CheckSsiNotDeployedByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetDeploymentMode(), DeploymentMode::Manual);
}

TEST_F(ConfigurationTest, CheckSsiIsDeployedIfEnvVarConstainsProfiler)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("tracer,profiler"));
    auto configuration = Configuration{};
    auto expectedValue = DeploymentMode::SingleStepInstrumentation;
    ASSERT_THAT(configuration.GetDeploymentMode(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDeployedIfEnvVarDoesNotContainProfiler)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("tracer"));
    auto configuration = Configuration{};
    auto expectedValue = DeploymentMode::SingleStepInstrumentation;
    ASSERT_THAT(configuration.GetDeploymentMode(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDeployedIfEnvVarIsEmpty)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr(""));
    auto configuration = Configuration{};
    auto expectedValue = DeploymentMode::SingleStepInstrumentation;
    ASSERT_THAT(configuration.GetDeploymentMode(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDeployedIfProfilerEnabledVarIsSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("profiler,tracer"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = DeploymentMode::SingleStepInstrumentation;
    ASSERT_THAT(configuration.GetDeploymentMode(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDeployedIfProfilerEnabledVarIsEmpty)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("profiler,tracer"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr(""));
    auto configuration = Configuration{};
    auto expectedValue = DeploymentMode::SingleStepInstrumentation;
    ASSERT_THAT(configuration.GetDeploymentMode(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDeployedIfProfilerEnabledVarIsSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("profiler,tracer"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = DeploymentMode::SingleStepInstrumentation;
    ASSERT_THAT(configuration.GetDeploymentMode(), expectedValue);
}


// with Stable Configuration, ALL enablement/SSI decisions are delayed at runtime so by default become disabled
// Use the DD_PROFILING_MANAGED_ACTIVATION_ENABLED kill switch to test per env vars configuration
TEST_F(ConfigurationTest, CheckStandbyIfSsiEnabledAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("tracer,profiler"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckStandbyIfSsiEnabledAndStableConfigurationEnabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::SsiDeployed, WStr("tracer,profiler"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckStandbyIfSsiEnvVarDoesNotContainProfilerAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr("tracer"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckStandbyIfSsiEnvVarEmptyAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiDeployed, WStr(""));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckSsiIsActivatedIfProfilerEnvVarConstainsAutoAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("auto"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsNotSetAndStableConfigurationByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckProfilerIsDisabledIfEnvVarIsEmptyAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("  "));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsToTrueAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("1 ")); // add a space on purpose to ensure that it's correctly parsed
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsNotParsableAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("not_parsable_ "));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsToFalseAndStableConfigurationByDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ProfilerEnabled, WStr("  0 ")); // add a space on purpose to ensure that it's correctly parsed
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::Standby);
}

// use the Stable Configuration kill switch to validate per env vars enablement configuration
TEST_F(ConfigurationTest, CheckNoMoreSupportedSsiActivationModeIfEnvVarConstainsProfiler)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::SsiDeployed, WStr("profiler"));
    auto configuration = Configuration{};
    auto expectedValue = EnablementStatus::NotSet;
    ASSERT_THAT(configuration.GetEnablementStatus(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDisableddIfEnvVarDoesNotContainProfiler)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::SsiDeployed, WStr("tracer"));
    auto configuration = Configuration{};
    auto expectedValue = EnablementStatus::NotSet;
    ASSERT_THAT(configuration.GetEnablementStatus(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsDisabledIfEnvVarIsEmpty)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::SsiDeployed, WStr(""));
    auto configuration = Configuration{};
    auto expectedValue = EnablementStatus::NotSet;
    ASSERT_THAT(configuration.GetEnablementStatus(), expectedValue);
}

TEST_F(ConfigurationTest, CheckSsiIsActivatedIfProfilerEnvVarConstainsAuto)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("auto"));
    auto configuration = Configuration{};
    auto expectedValue = EnablementStatus::Auto;
    ASSERT_THAT(configuration.GetEnablementStatus(), expectedValue);
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsNotSet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::NotSet) << "Env var is not set. Profiler enablement should be the default one.";
}

TEST_F(ConfigurationTest, CheckProfilerIsDisabledIfEnvVarIsEmpty)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("  "));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::ManuallyDisabled);
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("1 ")); // add a space on purpose to ensure that it's correctly parsed
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::ManuallyEnabled) << "Env var is to 1. Profiler must be enabled.";
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsNotParsable)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("not_parsable_ "));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::ManuallyDisabled) << "Profiler must disabled is value for DD_PROLIFER_ENABLED cannot be converted to bool.";
}

TEST_F(ConfigurationTest, CheckProfilerEnablementIfEnvVarIsToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ManagedActivationEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ar2(EnvironmentVariables::ProfilerEnabled, WStr("  0 ")); // add a space on purpose to ensure that it's correctly parsed
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetEnablementStatus(), EnablementStatus::ManuallyDisabled) << "Env var is to 0. Profiler must be disabled.";
}

TEST_F(ConfigurationTest, CheckEtwLoggingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsEtwLoggingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckEtwLoggingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::EtwLoggingEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef LINUX
        false;
#else
        true;
#endif
    ASSERT_THAT(configuration.IsEtwLoggingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckDefaultCpuProfilerType)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilerType, WStr(""));
    auto configuration = Configuration{};
    auto expected =
#ifdef _WINDOWS
        CpuProfilerType::ManualCpuTime;
#else
        CpuProfilerType::TimerCreate;
#endif
    ASSERT_THAT(configuration.GetCpuProfilerType(), expected);
}

TEST_F(ConfigurationTest, CheckDefaultCpuProfilerTypeWhenEnvVarNotSet)
{
    auto configuration = Configuration{};
    auto expected =
#ifdef _WINDOWS
        CpuProfilerType::ManualCpuTime;
#else
        CpuProfilerType::TimerCreate;
#endif
    ASSERT_THAT(configuration.GetCpuProfilerType(), expected);
}

TEST_F(ConfigurationTest, CheckUnknownCpuProfilerType)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilerType, WStr("UnknownCpuProfilerType"));
    auto configuration = Configuration{};
    auto expected =
#ifdef _WINDOWS
        CpuProfilerType::ManualCpuTime;
#else
        CpuProfilerType::TimerCreate;
#endif
    ASSERT_THAT(configuration.GetCpuProfilerType(), expected);
}

TEST_F(ConfigurationTest, CheckManualCpuProfilerType)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilerType, WStr("ManualCpuTime"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCpuProfilerType(), CpuProfilerType::ManualCpuTime);
}

TEST_F(ConfigurationTest, CheckTimerCreateCpuProfilerType)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilerType, WStr("TimerCreate"));
    auto configuration = Configuration{};
    auto expected =
#ifdef LINUX
        CpuProfilerType::TimerCreate;
#else
        CpuProfilerType::ManualCpuTime;
#endif
    ASSERT_THAT(configuration.GetCpuProfilerType(), expected);
}

TEST_F(ConfigurationTest, CheckDefaultCpuProfilingInterval)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingInterval, WStr(""));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCpuProfilingInterval(), 9ms);
}

TEST_F(ConfigurationTest, CheckCpuProfilingIntervalSetInEnvVar)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingInterval, WStr("42"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCpuProfilingInterval(), 42ms);
}

TEST_F(ConfigurationTest, CheckCpuProfilingIntervalIsNotBelowDefault)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::CpuProfilingInterval, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetCpuProfilingInterval(), 9ms);
}

TEST_F(ConfigurationTest, CheckLongLivedThresholdWhenEnvVarNotSet)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetSsiLongLivedThreshold(), 30'000ms);
}

TEST_F(ConfigurationTest, CheckLongLivedThresholdWhenEnvVarNotParsable)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiLongLivedThreshold, WStr("not_an_int"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetSsiLongLivedThreshold(), 30'000ms);
}

TEST_F(ConfigurationTest, CheckLongLivedThresholdWhenEnvVarIsCorrectlySet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiLongLivedThreshold, WStr("42001"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetSsiLongLivedThreshold(), 42'001ms);
}

TEST_F(ConfigurationTest, CheckLongLivedThresholdIsSetToZero)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiLongLivedThreshold, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetSsiLongLivedThreshold(), 0ms);
}

TEST_F(ConfigurationTest, CheckLongLivedThresholdIsDefaultIfSetToNegativeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::SsiLongLivedThreshold, WStr("-1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetSsiLongLivedThreshold(), 30'000ms);
}

TEST_F(ConfigurationTest, CheckHttpRequestThresholdWhenEnvVarNotSet)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetHttpRequestDurationThreshold(), 50ms);
}

TEST_F(ConfigurationTest, CheckHttpRequestThresholdWhenEnvVarNotParsable)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpRequestDurationThreshold, WStr("not_an_int"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetHttpRequestDurationThreshold(), 50ms);
}

TEST_F(ConfigurationTest, CheckHttpRequestThresholdWhenEnvVarIsCorrectlySet)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpRequestDurationThreshold, WStr("456"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetHttpRequestDurationThreshold(), 456ms);
}

TEST_F(ConfigurationTest, CheckHttpRequestThresholdIsSetToZero)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpRequestDurationThreshold, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetHttpRequestDurationThreshold(), 0ms);
}

TEST_F(ConfigurationTest, CheckHttpRequestThresholdIsDefaultIfSetToNegativeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpRequestDurationThreshold, WStr("-1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.GetHttpRequestDurationThreshold(), 50ms);
}

TEST_F(ConfigurationTest, CheckHttpProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsHttpProfilingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckHttpProfilingIsDisabledIfEnvVarIsDisabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsHttpProfilingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckHttpProfilingIsEnabledIfEnvVarIsEnabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsHttpProfilingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckHttpProfilingEnabledTakesOverInternal)
{
    EnvironmentHelper::EnvironmentVariable ev1(EnvironmentVariables::HttpProfilingEnabled, WStr("1"));
    EnvironmentHelper::EnvironmentVariable ev2(EnvironmentVariables::HttpProfilingInternalEnabled, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsHttpProfilingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckHttpProfilingdisabledTakesOverInternal)
{
    EnvironmentHelper::EnvironmentVariable ev1(EnvironmentVariables::HttpProfilingEnabled, WStr("0"));
    EnvironmentHelper::EnvironmentVariable ev2(EnvironmentVariables::HttpProfilingInternalEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.IsHttpProfilingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckInternalHttpProfilingIsTakenIntoAccount)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HttpProfilingInternalEnabled, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.IsHttpProfilingEnabled(), expectedValue);
}

TEST_F(ConfigurationTest, CheckForceHttpSamplingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.ForceHttpSampling(), expectedValue);
}

TEST_F(ConfigurationTest, CheckForceHttpSamplingIsDisabledIfEnvVarIsDisabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ForceHttpSampling, WStr("0"));
    auto configuration = Configuration{};
    auto expectedValue = false;
    ASSERT_THAT(configuration.ForceHttpSampling(), expectedValue);
}

TEST_F(ConfigurationTest, CheckForceHttpSamplingIsEnabledIfEnvVarIsEnabled)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::ForceHttpSampling, WStr("1"));
    auto configuration = Configuration{};
    auto expectedValue = true;
    ASSERT_THAT(configuration.ForceHttpSampling(), expectedValue);
}

TEST_F(ConfigurationTest, CheckWaitHandleProfilingIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsWaitHandleProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckWaitHandleProfilingIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WaitHandleProfilingEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsWaitHandleProfilingEnabled(), true);
}

TEST_F(ConfigurationTest, CheckWaitHandleProfilingIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::WaitHandleProfilingEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsWaitHandleProfilingEnabled(), false);
}

TEST_F(ConfigurationTest, CheckHeapSnapshotIsDisabledByDefault)
{
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapSnapshotEnabled(), false);
}

TEST_F(ConfigurationTest, CheckHeapSnapshotIsEnabledIfEnvVarSetToTrue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapSnapshotEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapSnapshotEnabled(), true);
}

TEST_F(ConfigurationTest, CheckHeapSnapshotIsDisabledIfEnvVarSetToFalse)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapSnapshotEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_THAT(configuration.IsHeapSnapshotEnabled(), false);
}

TEST_F(ConfigurationTest, CheckHeapHandleLimitIfNoValue)
{
    auto configuration = Configuration{};
    auto threshold = configuration.GetHeapHandleLimit();
    ASSERT_THAT(threshold, 4096);
}

TEST_F(ConfigurationTest, CheckHeapHandleLimitIfTooSmallValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapHandleLimit, WStr("1"));
    auto configuration = Configuration{};
    auto threshold = configuration.GetHeapHandleLimit();
    ASSERT_THAT(threshold, 1024);
}

TEST_F(ConfigurationTest, CheckHeapHandleLimitIfTooLargeValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapHandleLimit, WStr("100000"));
    auto configuration = Configuration{};
    auto threshold = configuration.GetHeapHandleLimit();
    ASSERT_THAT(threshold, 16000);
}

TEST_F(ConfigurationTest, CheckHeapHandleLimitIfCorrectValue)
{
    EnvironmentHelper::EnvironmentVariable ar(EnvironmentVariables::HeapHandleLimit, WStr("8000"));
    auto configuration = Configuration{};
    auto threshold = configuration.GetHeapHandleLimit();
    ASSERT_THAT(threshold, 8000);
}
