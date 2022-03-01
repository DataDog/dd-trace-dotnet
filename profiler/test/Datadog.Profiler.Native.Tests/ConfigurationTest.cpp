// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"

#include "Configuration.h"
#include "EnvironmentVariables.h"
#include "OpSysTools.h"

#include "shared/src/native-src/string.h"

using namespace std::literals::chrono_literals;

extern void setenv(const shared::WSTRING& name, const shared::WSTRING& value);

extern void unsetenv(const shared::WSTRING& name);

class EnvironmentVariableAutoReset
{
public:
    EnvironmentVariableAutoReset(shared::WSTRING const& env, shared::WSTRING value) :
        _env{env}
    {
        setenv(_env, value);
    }
    ~EnvironmentVariableAutoReset()
    {
        unsetenv(_env);
    }

private:
    shared::WSTRING const& _env;
};

TEST(ConfigurationTest, CheckIfDebugLogIsNotEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::DebugLogEnabled);
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DebugLogEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DebugLogEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DebugLogEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfDebugLogIsEnabledWhenInDev)
{
    unsetenv(EnvironmentVariables::DebugLogEnabled);
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));

    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsDebugLogEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::NativeFramesEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsNativeFramesEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::NativeFramesEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsNativeFramesEnabled());
}

TEST(ConfigurationTest, CheckIfNativeFramesIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::NativeFramesEnabled, WStr(""));
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::OperationalMetricsEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsOperationalMetricsEnabled());
}

TEST(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::OperationalMetricsEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsOperationalMetricsEnabled());
}

TEST(ConfigurationTest, CheckIfOperationalMetricsIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::OperationalMetricsEnabled, WStr(""));
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
        WStr("/var/log/datadog/");
#endif
    ASSERT_EQ(expectedValue, configuration.GetLogDirectory());
}

TEST(ConfigurationTest, CheckLogDirectoryWhenVariableIsSet)
{
    auto expectedValue = fs::path(WStr("MyFolder/WhereIWantIt/ToBe"));
    EnvironmentVariableAutoReset ar(EnvironmentVariables::LogDirectory, shared::ToWSTRING(expectedValue.string()));
    auto configuration = Configuration{};
    ASSERT_EQ(expectedValue, configuration.GetLogDirectory());
}

TEST(ConfigurationTest, CheckDefaultPprofDirectoryWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::ProfilesOutputDir);
    auto configuration = Configuration{};
    auto expectedValue =
#ifdef _WINDOWS
        WStr("C:\\ProgramData\\Datadog-APM\\Pprof-files\\DotNet");
#else
        WStr("/var/log/datadog/pprof-files");
#endif
    ASSERT_EQ(expectedValue, configuration.GetProfilesOutputDirectory());
}

TEST(ConfigurationTest, CheckProfileDirectoryWhenVariableIsSet)
{
    auto expectedValue = fs::path(WStr("MyFolder/WhereIWantIt/ToBe"));
    EnvironmentVariableAutoReset ar(EnvironmentVariables::ProfilesOutputDir, shared::ToWSTRING(expectedValue.string()));
    auto configuration = Configuration{};
    ASSERT_EQ(expectedValue, configuration.GetProfilesOutputDirectory());
}

TEST(ConfigurationTest, CheckDefaultUploadIntervalInDevMode)
{
    unsetenv(EnvironmentVariables::UploadInterval);
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_EQ(20s, configuration.GetUploadInterval());
}

TEST(ConfigurationTest, CheckDefaultUploadIntervalInNonDevMode)
{
    unsetenv(EnvironmentVariables::UploadInterval);
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DevelopmentConfiguration, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_EQ(60s, configuration.GetUploadInterval());
}

TEST(ConfigurationTest, CheckUploadIntervalWhenVariableIsSet)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::UploadInterval, WStr("200"));
    auto configuration = Configuration{};
    ASSERT_EQ(200s, configuration.GetUploadInterval());
}

TEST(ConfigurationTest, CheckDefaultSiteInDevMode)
{
    unsetenv(EnvironmentVariables::Site);
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DevelopmentConfiguration, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_EQ("datad0g.com", configuration.GetSite());
}

TEST(ConfigurationTest, CheckDefaultSiteInNonDevMode)
{
    unsetenv(EnvironmentVariables::Site);
    EnvironmentVariableAutoReset ar(EnvironmentVariables::DevelopmentConfiguration, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_EQ("datadoghq.com", configuration.GetSite());
}

TEST(ConfigurationTest, CheckSiteWhenVariableIsSet)
{
    auto expectedValue = WStr("MySite");
    EnvironmentVariableAutoReset ar(EnvironmentVariables::Site, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::Version, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::Environment, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::Environment, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::AgentUrl, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::AgentHost, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::AgentPort, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::ApiKey, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::ServiceName, expectedValue);
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
    EnvironmentVariableAutoReset ar(EnvironmentVariables::Tags, WStr("foo:bar,lab1:val1"));
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::ContainerEq(tags{{"foo", "bar"}, {"lab1", "val1"}}));
}

TEST(ConfigurationTest, CheckUserTagsWhenVariableIsSetWithIncompleteTag)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::Tags, WStr("foo:bar,foobar:barbar,lab1:"));
    auto configuration = Configuration{};
    EXPECT_THAT(configuration.GetUserTags(), ::testing::ContainerEq(tags{{"foo", "bar"}, {"foobar", "barbar"}, {"lab1", ""}}));
}


TEST(ConfigurationTest, CheckThatFFIsLibddprofIsEnabledWhenEnvVariableIsSetToTrue)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::FF_LibddprofEnabled, WStr("true"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsFFLibddprofEnabled());
}

TEST(ConfigurationTest, CheckThatFFIsLibddprofIsEnabledWhenEnvVariableIsSetToOne)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::FF_LibddprofEnabled, WStr("1"));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsFFLibddprofEnabled());
}

TEST(ConfigurationTest, CheckThatFFIsLibddprofIsDisabledWhenEnvVariableIsSetToFalse)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::FF_LibddprofEnabled, WStr("false"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsFFLibddprofEnabled());
}

TEST(ConfigurationTest, CheckThatFFIsLibddprofIsDisabledWhenEnvVariableIsSetToZero)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::FF_LibddprofEnabled, WStr("0"));
    auto configuration = Configuration{};
    ASSERT_FALSE(configuration.IsFFLibddprofEnabled());
}

TEST(ConfigurationTest, CheckThatFFIsLibddprofIsEnabledWhenEnvVariableIsSetEmptyString)
{
    EnvironmentVariableAutoReset ar(EnvironmentVariables::FF_LibddprofEnabled, WStr(""));
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsFFLibddprofEnabled());
}

TEST(ConfigurationTest, CheckThatFFIsLibddprofIsEnabledWhenVariableIsNotSet)
{
    unsetenv(EnvironmentVariables::FF_LibddprofEnabled);
    auto configuration = Configuration{};
    ASSERT_TRUE(configuration.IsFFLibddprofEnabled());
}
