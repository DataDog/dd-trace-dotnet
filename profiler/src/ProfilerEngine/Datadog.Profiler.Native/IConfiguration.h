// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <chrono>
#include <string>
#include <tuple>
#include <vector>
#include <cstdint>

#include "TagsHelper.h"

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"
#include "shared/src/native-src/string.h"

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
    virtual int32_t GetAgentPort() const = 0;
    virtual bool IsAgentless() const = 0;
    virtual std::string const& GetSite() const = 0;
    virtual std::string const& GetApiKey() const = 0;
    virtual std::string const& GetServiceName() const = 0;
    virtual tags const& GetUserTags() const = 0;
    virtual bool IsCpuProfilingEnabled() const = 0;
    virtual bool IsWallTimeProfilingEnabled() const = 0;
    virtual bool IsExceptionProfilingEnabled() const = 0;
    virtual int32_t ExceptionSampleLimit() const = 0;
    virtual bool IsAllocationProfilingEnabled() const = 0;
    virtual bool IsContentionProfilingEnabled() const = 0;
    virtual double MinimumCores() const = 0;
    virtual int32_t AllocationSampleLimit() const = 0;
    virtual int32_t ContentionSampleLimit() const = 0;
    virtual int32_t ContentionDurationThreshold() const = 0;
    virtual std::chrono::nanoseconds CpuWallTimeSamplingRate() const = 0;
    virtual const std::string& GetNamedPipeName() const = 0;
    virtual bool IsTimestampsAsLabelEnabled() const = 0;
    virtual int32_t WalltimeThreadsThreshold() const = 0;
    virtual int32_t CpuThreadsThreshold() const = 0;
    virtual int32_t CodeHotspotsThreadsThreshold() const = 0;
    virtual bool IsGarbageCollectionProfilingEnabled() const = 0;
    virtual bool IsHeapProfilingEnabled() const = 0;
    virtual bool UseBacktrace2() const = 0;
    virtual bool IsAllocationRecorderEnabled() const = 0;
    virtual bool IsDebugInfoEnabled() const = 0;
    virtual bool IsGcThreadsCpuTimeEnabled() const = 0;
    virtual bool IsThreadLifetimeEnabled() const = 0;
    virtual std::string const& GetGitRepositoryUrl() const = 0;
    virtual std::string const& GetGitCommitSha() const = 0;
    virtual bool IsInternalMetricsEnabled() const = 0;
    virtual bool IsSystemCallsShieldEnabled() const = 0;
    virtual bool IsCIVisibilityEnabled() const = 0;
    virtual std::uint64_t GetCIVisibilitySpanId() const = 0;
};
