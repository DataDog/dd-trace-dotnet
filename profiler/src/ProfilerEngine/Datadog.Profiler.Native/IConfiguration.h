// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <chrono>
#include <filesystem>
#include <string>
#include <tuple>
#include <vector>

#include "TagsHelper.h"

#include "shared/src/native-src/string.h"

namespace fs = std::filesystem;

class IConfiguration
{
public:
    virtual ~IConfiguration() = default;
    virtual bool IsDebugLogEnabled() const = 0;
    virtual fs::path const& GetLogDirectory() const = 0;
    virtual fs::path const& GetProfilesOutputDirectory() const = 0;
    virtual bool IsNativeFramesEnabled() const = 0;
    virtual bool IsOperationalMetricsEnabled() const = 0;
    virtual std::chrono::seconds GetUploadInterval() const = 0;
    virtual std::string const& GetVersion() const = 0;
    virtual std::string const& GetEnvironment() const = 0;
    virtual std::string const& GetHostname() const = 0;
    virtual std::string const& GetAgentUrl() const = 0;
    virtual std::string const& GetAgentHost() const = 0;
    virtual int GetAgentPort() const = 0;
    virtual bool IsAgentless() const = 0;
    virtual std::string const& GetSite() const = 0;
    virtual std::string const& GetApiKey() const = 0;
    virtual std::string const& GetServiceName() const = 0;
    virtual tags const& GetUserTags() const = 0;

    // feature flags
    virtual bool IsFFLibddprofEnabled() const = 0;
};