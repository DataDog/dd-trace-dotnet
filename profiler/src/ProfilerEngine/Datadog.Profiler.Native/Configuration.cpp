// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Configuration.h"

#include "TagsHelper.h"

#include <type_traits>

#include "EnvironmentVariables.h"
#include "OpSysTools.h"

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"
#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

using namespace std::literals::chrono_literals;

std::string const Configuration::DefaultDevSite = "datad0g.com";
std::string const Configuration::DefaultProdSite = "datadoghq.com";
std::string const Configuration::DefaultVersion = "Unspecified-Version";
std::string const Configuration::DefaultEnvironment = "Unspecified-Environment";
std::string const Configuration::DefaultAgentHost = "localhost";
int const Configuration::DefaultAgentPort = 8126;
std::string const Configuration::DefaultEmptyString = "";
std::chrono::seconds const Configuration::DefaultDevUploadInterval = 20s;
std::chrono::seconds const Configuration::DefaultProdUploadInterval = 60s;

Configuration::Configuration()
{
    _debugLogEnabled = GetEnvironmentValue(EnvironmentVariables::DebugLogEnabled, GetDefaultDebugLogEnabled());
    _logDirectory = ExtractLogDirectory();
    _pprofDirectory = ExtractPprofDirectory();
    _isOperationalMetricsEnabled = GetEnvironmentValue(EnvironmentVariables::OperationalMetricsEnabled, false);
    _isNativeFrameEnabled = GetEnvironmentValue(EnvironmentVariables::NativeFramesEnabled, false);
    _isCpuProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::CpuProfilingEnabled, false);
    _isWallTimeProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::WallTimeProfilingEnabled, true);
    _isExceptionProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::ExceptionProfilingEnabled, false);
    _uploadPeriod = ExtractUploadInterval();
    _userTags = ExtractUserTags();
    _version = GetEnvironmentValue(EnvironmentVariables::Version, DefaultVersion);
    _environmentName = GetEnvironmentValue(EnvironmentVariables::Environment, DefaultEnvironment);
    _hostname = GetEnvironmentValue(EnvironmentVariables::Hostname, OpSysTools::GetHostname());
    _agentUrl = GetEnvironmentValue(EnvironmentVariables::AgentUrl, DefaultEmptyString);
    _agentHost = GetEnvironmentValue(EnvironmentVariables::AgentHost, DefaultAgentHost);
    _agentPort = GetEnvironmentValue(EnvironmentVariables::AgentPort, DefaultAgentPort);
    _site = ExtractSite();
    _apiKey = GetEnvironmentValue(EnvironmentVariables::ApiKey, DefaultEmptyString);
    _serviceName = GetEnvironmentValue(EnvironmentVariables::ServiceName, OpSysTools::GetProcessName());
    _isAgentLess = GetEnvironmentValue(EnvironmentVariables::Agentless, false);
    _exceptionSampleLimit = GetEnvironmentValue(EnvironmentVariables::ExceptionSampleLimit, 100);
}

fs::path Configuration::ExtractLogDirectory()
{
    auto value = shared::GetEnvironmentValue(EnvironmentVariables::LogDirectory);
    if (value.empty())
        return GetDefaultLogDirectoryPath();

    return fs::path(value);
}

fs::path const& Configuration::GetLogDirectory() const
{
    return _logDirectory;
}

fs::path Configuration::ExtractPprofDirectory()
{
    auto value = shared::GetEnvironmentValue(EnvironmentVariables::ProfilesOutputDir);
    if (value.empty())
        return fs::path();

    return fs::path(value);
}

fs::path const& Configuration::GetProfilesOutputDirectory() const
{
    return _pprofDirectory;
}

bool Configuration::IsOperationalMetricsEnabled() const
{
    return _isOperationalMetricsEnabled;
}

bool Configuration::IsNativeFramesEnabled() const
{
    return _isNativeFrameEnabled;
}

bool Configuration::IsCpuProfilingEnabled() const
{
    return _isCpuProfilingEnabled;
}

bool Configuration::IsWallTimeProfilingEnabled() const
{
    return _isWallTimeProfilingEnabled;
}

bool Configuration::IsExceptionProfilingEnabled() const
{
    return _isExceptionProfilingEnabled;
}

int Configuration::ExceptionSampleLimit() const
{
    return _exceptionSampleLimit;
}

std::chrono::seconds Configuration::GetUploadInterval() const
{
    return _uploadPeriod;
}

tags const& Configuration::GetUserTags() const
{
    return _userTags;
}

bool Configuration::IsDebugLogEnabled() const
{
    return _debugLogEnabled;
}

std::string const& Configuration::GetVersion() const
{
    return _version;
}

std::string const& Configuration::GetEnvironment() const
{
    return _environmentName;
}

std::string const& Configuration::GetHostname() const
{
    return _hostname;
}

std::string const& Configuration::GetAgentUrl() const
{
    return _agentUrl;
}

std::string const& Configuration::GetAgentHost() const
{
    return _agentHost;
}

int Configuration::GetAgentPort() const
{
    return _agentPort;
}

std::string const& Configuration::GetSite() const
{
    return _site;
}

std::string const& Configuration::GetApiKey() const
{
    return _apiKey;
}

std::string const& Configuration::GetServiceName() const
{
    return _serviceName;
}

fs::path Configuration::GetApmBaseDirectory()
{
#ifdef _WINDOWS
    WCHAR output[MAX_PATH] = {0};
    auto result = ExpandEnvironmentStrings(WStr("%PROGRAMDATA%"), output, MAX_PATH);
    if (result != 0)
    {
        return fs::path(output);
    }

    return fs::path();
#else
    return fs::path(WStr("/var/log/datadog/"));
#endif
}

fs::path Configuration::GetDefaultLogDirectoryPath()
{
    auto baseDirectory = fs::path(GetApmBaseDirectory());
#ifdef _WINDOWS
    return baseDirectory / WStr(R"(Datadog-APM\logs\DotNet)");
#else
    return baseDirectory / WStr("dotnet");
#endif
}

tags Configuration::ExtractUserTags()
{
    return TagsHelper::Parse(shared::ToString(shared::GetEnvironmentValue(EnvironmentVariables::Tags)));
}

std::string Configuration::GetDefaultSite()
{
    auto isDev = GetEnvironmentValue(EnvironmentVariables::DevelopmentConfiguration, false);

    if (isDev)
    {
        return DefaultDevSite;
    }

    return DefaultProdSite;
}

std::string Configuration::ExtractSite()
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::Site);

    if (r.empty())
        return GetDefaultSite();

    return shared::ToString(r);
}

std::chrono::seconds Configuration::GetDefaultUploadInterval()
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::DevelopmentConfiguration);

    bool isDev;
    if (shared::TryParseBooleanEnvironmentValue(r, isDev) && isDev)
        return DefaultDevUploadInterval;
    return DefaultProdUploadInterval;
}

//
// shared::TryParse does not work on Linux
// not found the issue yet.
// For now, replace shared::TryParse by this function
// TODO Once in the Tracer repo:
// - replace shared::TryParse by this implementation
// - add tests

bool TryParse(shared::WSTRING const& s, int& result)
{
    auto str = shared::ToString(s);
    if (str == "")
    {
        result = 0;
        return false;
    }

    try
    {
        result = std::stoi(str);
        return true;
    }
    catch (std::exception const&)
    {
        // TODO log
    }
    result = 0;
    return false;
}

std::chrono::seconds Configuration::ExtractUploadInterval()
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::UploadInterval);
    int interval;
    if (TryParse(r, interval))
    {
        return std::chrono::seconds(interval);
    }

    return GetDefaultUploadInterval();
}

bool Configuration::GetDefaultDebugLogEnabled()
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::DevelopmentConfiguration);

    bool isDev;
    return shared::TryParseBooleanEnvironmentValue(r, isDev) && isDev;
}

bool Configuration::IsAgentless() const
{
    return _isAgentLess;
}

bool convert_to(shared::WSTRING const& s, bool& result)
{
    return shared::TryParseBooleanEnvironmentValue(s, result);
}

bool convert_to(shared::WSTRING const& s, std::string& result)
{
    result = shared::ToString(s);
    return true;
}

bool convert_to(shared::WSTRING const& s, shared::WSTRING& result)
{
    result = s;
    return true;
}

bool convert_to(shared::WSTRING const& s, int& result)
{
    return TryParse(s, result);
}

template <typename T>
T Configuration::GetEnvironmentValue(shared::WSTRING const& name, T const& defaultValue)
{
    auto r = shared::Trim(shared::GetEnvironmentValue(name));
    if (r.empty()) return defaultValue;
    T result{};
    if (!convert_to(r, result)) return std::move(defaultValue);
    return result;
}