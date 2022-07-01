// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include <chrono>
#include <memory>
#include <string>

#include "IConfiguration.h"
#include "TagsHelper.h"
#include "shared/src/native-src/string.h"

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

class Configuration final : public IConfiguration
{
public:
    Configuration();
    ~Configuration() override = default;

    fs::path const& GetLogDirectory() const override;
    fs::path const& GetProfilesOutputDirectory() const override;
    bool IsOperationalMetricsEnabled() const override;
    bool IsNativeFramesEnabled() const override;
    std::chrono::seconds GetUploadInterval() const override;
    tags const& GetUserTags() const override;
    bool IsDebugLogEnabled() const override;

    std::string const& GetVersion() const override;
    std::string const& GetEnvironment() const override;
    std::string const& GetHostname() const override;
    std::string const& GetAgentUrl() const override;
    std::string const& GetAgentHost() const override;
    int GetAgentPort() const override;
    bool IsAgentless() const override;
    std::string const& GetSite() const override;
    std::string const& GetApiKey() const override;
    std::string const& GetServiceName() const override;
    bool IsCpuProfilingEnabled() const override;
    bool IsWallTimeProfilingEnabled() const override;
    bool IsExceptionProfilingEnabled() const override;
    int ExceptionSampleLimit() const override;

private:
    static tags ExtractUserTags();
    static std::string GetDefaultSite();
    static std::string ExtractSite();
    static std::chrono::seconds ExtractUploadInterval();
    static fs::path GetDefaultLogDirectoryPath();
    static fs::path GetApmBaseDirectory();
    static fs::path ExtractLogDirectory();
    static fs::path ExtractPprofDirectory();
    static std::chrono::seconds GetDefaultUploadInterval();
    static bool GetDefaultDebugLogEnabled();
    template <typename T>
    static T GetEnvironmentValue(shared::WSTRING const& name, T const& defaultValue);

private:
    static std::string const DefaultProdSite;
    static std::string const DefaultDevSite;
    static std::string const DefaultVersion;
    static std::string const DefaultEnvironment;
    static std::string const DefaultAgentHost;
    static std::string const DefaultEmptyString;
    static int const DefaultAgentPort;
    static std::chrono::seconds const DefaultDevUploadInterval;
    static std::chrono::seconds const DefaultProdUploadInterval;

    bool _isProfilingEnabled;
    bool _isCpuProfilingEnabled;
    bool _isWallTimeProfilingEnabled;
    bool _isExceptionProfilingEnabled;
    bool _debugLogEnabled;
    fs::path _logDirectory;
    fs::path _pprofDirectory;
    bool _isOperationalMetricsEnabled;
    std::string _version;
    std::string _serviceName;
    std::string _environmentName;
    std::chrono::seconds _uploadPeriod;
    std::string _agentUrl;
    std::string _agentHost;
    std::int32_t _agentPort;
    std::string _apiKey;
    std::string _hostname;
    std::string _site;
    tags _userTags;
    bool _isNativeFrameEnabled;
    bool _isAgentLess;
    int _exceptionSampleLimit;
};
