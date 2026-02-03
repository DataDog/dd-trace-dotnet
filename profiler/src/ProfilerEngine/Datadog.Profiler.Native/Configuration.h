// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include <chrono>
#include <memory>
#include <string>

#include "DeploymentMode.h"
#include "EnablementStatus.h"
#include "CpuProfilerType.h"
#include "IConfiguration.h"
#include "TagsHelper.h"
#include "shared/src/native-src/string.h"

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

using namespace std::literals::chrono_literals;

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
    int32_t GetAgentPort() const override;
    bool IsAgentless() const override;
    std::string const& GetSite() const override;
    std::string const& GetApiKey() const override;
    std::string const& GetServiceName() const override;
    bool IsCpuProfilingEnabled() const override;
    bool IsWallTimeProfilingEnabled() const override;
    bool IsExceptionProfilingEnabled() const override;
    int32_t ExceptionSampleLimit() const override;
    bool IsAllocationProfilingEnabled() const override;
    bool IsContentionProfilingEnabled() const override;
    double MinimumCores() const override;
    int32_t AllocationSampleLimit() const override;
    int32_t ContentionSampleLimit() const override;
    int32_t ContentionDurationThreshold() const override;
    std::chrono::nanoseconds CpuWallTimeSamplingRate() const override;
    const std::string& GetNamedPipeName() const override;
    bool IsTimestampsAsLabelEnabled() const override;
    int32_t WalltimeThreadsThreshold() const override;
    int32_t CpuThreadsThreshold() const override;
    int32_t CodeHotspotsThreadsThreshold() const override;
    bool IsGarbageCollectionProfilingEnabled() const override;
    bool IsHeapProfilingEnabled() const override;
    bool IsAllocationRecorderEnabled() const override;
    bool IsDebugInfoEnabled() const override;
    bool IsGcThreadsCpuTimeEnabled() const override;
    bool IsThreadLifetimeEnabled() const override;
    std::string const& GetGitRepositoryUrl() const override;
    std::string const& GetGitCommitSha() const override;
    bool IsInternalMetricsEnabled() const override;
    bool IsSystemCallsShieldEnabled() const override;
    bool IsCIVisibilityEnabled() const override;
    std::uint64_t GetCIVisibilitySpanId() const override;
    bool IsEtwEnabled() const override;
    bool IsEtwLoggingEnabled() const override;
    std::string const& GetEtwReplayEndpoint() const override;
    EnablementStatus GetEnablementStatus() const override;
    DeploymentMode GetDeploymentMode() const override;
    std::chrono::milliseconds GetSsiLongLivedThreshold() const override;
    CpuProfilerType GetCpuProfilerType() const override;
    std::chrono::milliseconds GetCpuProfilingInterval() const override;
    bool IsHttpProfilingEnabled() const override;
    std::chrono::milliseconds GetHttpRequestDurationThreshold() const override;
    bool ForceHttpSampling() const override;
    bool IsWaitHandleProfilingEnabled() const override;
    bool IsManagedActivationEnabled() const override;
    void SetEnablementStatus(EnablementStatus status) override;
    bool IsHeapSnapshotEnabled() const override;
    std::chrono::minutes GetHeapSnapshotInterval() const override;
    std::chrono::milliseconds GetHeapSnapshotCheckInterval() const override;
    uint32_t GetHeapSnapshotMemoryPressureThreshold() const override;
    uint32_t GetHeapHandleLimit() const override;
    bool UseManagedCodeCache() const override;

private:
    static tags ExtractUserTags();
    static std::string GetDefaultSite();
    static std::string ExtractSite();
    static std::chrono::seconds ExtractUploadInterval();
    static std::chrono::milliseconds ExtractCpuProfilingInterval(std::chrono::milliseconds minimum = DefaultCpuProfilingInterval);
    static fs::path GetDefaultLogDirectoryPath();
    static fs::path GetApmBaseDirectory();
    static fs::path ExtractLogDirectory();
    static fs::path ExtractPprofDirectory();
    static std::chrono::seconds GetDefaultUploadInterval();
    static bool GetDefaultDebugLogEnabled();
    template <typename T>
    static T GetEnvironmentValue(shared::WSTRING const& name, T const& defaultValue, bool shouldLog = false);
    template <typename T>
    static bool IsEnvironmentValueSet(shared::WSTRING const& name, T& value);
    static std::chrono::nanoseconds ExtractCpuWallTimeSamplingRate(int minimum = 5);
    static int32_t ExtractWallTimeThreadsThreshold();
    static int32_t ExtractCpuThreadsThreshold();
    static int32_t ExtractCodeHotspotsThreadsThreshold();
    static bool GetContention();
    EnablementStatus ExtractEnablementStatus();
    std::chrono::milliseconds ExtractSsiLongLivedThreshold() const;
    std::chrono::milliseconds ExtractHttpRequestDurationThreshold() const;
    std::chrono::minutes ExtractHeapSnapshotInterval() const;
    std::chrono::milliseconds ExtractHeapSnapshotCheckInterval() const;
    std::chrono::minutes GetDefaultHeapSnapshotInterval() const;
    int32_t ExtractHeapHandleLimit() const;

private:
    static std::string const DefaultProdSite;
    static std::string const DefaultDevSite;
    static std::string const DefaultVersion;
    static std::string const DefaultEnvironment;
    static std::string const DefaultAgentHost;
    static std::string const DefaultEmptyString;
    static int32_t const DefaultAgentPort;
    static std::chrono::seconds const DefaultDevUploadInterval;
    static std::chrono::seconds const DefaultProdUploadInterval;
    static std::chrono::milliseconds const DefaultCpuProfilingInterval;
    static CpuProfilerType const DefaultCpuProfilerType;
    static std::chrono::minutes const DefaultDevHeapSnapshotInterval;
    static std::chrono::minutes const DefaultProdHeapSnapshotInterval;

    bool _isProfilingEnabled;
    bool _isCpuProfilingEnabled;
    bool _isWallTimeProfilingEnabled;
    bool _isExceptionProfilingEnabled;
    bool _isAllocationProfilingEnabled;
    bool _isContentionProfilingEnabled;
    bool _isGarbageCollectionProfilingEnabled;
    bool _isHeapProfilingEnabled;
    bool _isThreadLifetimeEnabled;
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
    int32_t _exceptionSampleLimit;
    int32_t _allocationSampleLimit;
    int32_t _contentionSampleLimit;
    int32_t _contentionDurationThreshold;
    std::chrono::nanoseconds _cpuWallTimeSamplingRate;
    int32_t _walltimeThreadsThreshold;
    int32_t _cpuThreadsThreshold;
    int32_t _codeHotspotsThreadsThreshold;
    uint32_t _heapHandleLimit;
    bool _isAllocationRecorderEnabled;
    bool _isGcThreadsCpuTimeEnabled;
    std::string _gitRepositoryUrl;
    std::string _gitCommitSha;

    double _minimumCores;
    std::string _namedPipeName;
    bool _isTimestampsAsLabelEnabled;
    bool _isDebugInfoEnabled;
    bool _isInternalMetricsEnabled;
    bool _isSystemCallsShieldEnabled;

    bool _isCIVisibilityEnabled;
    std::uint64_t _internalCIVisibilitySpanId;
    bool _isEtwEnabled;
    DeploymentMode _deploymentMode;
    bool _isManagedActivationEnabled;
    bool _isEtwLoggingEnabled;
    std::string _etwReplayEndpoint;
    EnablementStatus _enablementStatus;
    std::chrono::milliseconds _ssiLongLivedThreshold;
    bool _isHttpProfilingEnabled;
    std::chrono::milliseconds _httpRequestDurationThreshold;
    bool _forceHttpSampling;

    CpuProfilerType _cpuProfilerType;
    std::chrono::milliseconds _cpuProfilingInterval;
    bool _isWaitHandleProfilingEnabled;

    bool _isHeapSnapshotEnabled;
    std::chrono::minutes _heapSnapshotInterval;
    std::chrono::milliseconds _heapSnapshotCheckInterval;
    uint32_t _heapSnapshotMemoryPressureThreshold; // in % of used memory
    bool _useManagedCodeCache;
};
