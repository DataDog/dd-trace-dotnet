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
        WStr("C:\\ProgramData\\Datadog-APM\\logs\\DotNet");
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