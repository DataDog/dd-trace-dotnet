// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Configuration.h"

#include "TagsHelper.h"

#include <type_traits>

#include "EnvironmentVariables.h"
#include "Log.h"
#include "OpSysTools.h"

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"
#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

std::string const Configuration::DefaultDevSite = "datad0g.com";
std::string const Configuration::DefaultProdSite = "datadoghq.com";
std::string const Configuration::DefaultVersion = "Unspecified-Version";
std::string const Configuration::DefaultEnvironment = "Unspecified-Environment";
std::string const Configuration::DefaultAgentHost = "localhost";
int32_t const Configuration::DefaultAgentPort = 8126;
std::string const Configuration::DefaultEmptyString = "";
std::chrono::seconds const Configuration::DefaultDevUploadInterval = 20s;
std::chrono::seconds const Configuration::DefaultProdUploadInterval = 60s;
std::chrono::milliseconds const Configuration::DefaultCpuProfilingInterval = 9ms;
CpuProfilerType const Configuration::DefaultCpuProfilerType =
#ifdef _WINDOWS
    CpuProfilerType::ManualCpuTime;
#else
    CpuProfilerType::TimerCreate;
#endif
std::chrono::minutes const Configuration::DefaultDevHeapSnapshotInterval = 1min;
std::chrono::minutes const Configuration::DefaultProdHeapSnapshotInterval = 5min;

Configuration::Configuration()
{
    _debugLogEnabled = GetEnvironmentValue(EnvironmentVariables::DebugLogEnabled, GetDefaultDebugLogEnabled());
    _logDirectory = ExtractLogDirectory();
    _pprofDirectory = ExtractPprofDirectory();
    _isOperationalMetricsEnabled = GetEnvironmentValue(EnvironmentVariables::OperationalMetricsEnabled, false);
    _isNativeFrameEnabled = GetEnvironmentValue(EnvironmentVariables::NativeFramesEnabled, false);
    _isCpuProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::CpuProfilingEnabled, true, true);
    _isWallTimeProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::WallTimeProfilingEnabled, true, true);
    _isExceptionProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::ExceptionProfilingEnabled, true, true);
    _isAllocationProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::AllocationProfilingEnabled, false, true);
    _isContentionProfilingEnabled = GetContention();
    _isGarbageCollectionProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::GCProfilingEnabled, true, true);
    _isHeapProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::HeapProfilingEnabled, false, true);
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
    _serviceName = GetEnvironmentValue(EnvironmentVariables::ServiceName, OpSysTools::GetProcessName(), true);
    _isAgentLess = GetEnvironmentValue(EnvironmentVariables::Agentless, false);
    _exceptionSampleLimit = GetEnvironmentValue(EnvironmentVariables::ExceptionSampleLimit, 500);
    _allocationSampleLimit = GetEnvironmentValue(EnvironmentVariables::AllocationSampleLimit, 2000);
    _contentionSampleLimit = GetEnvironmentValue(EnvironmentVariables::ContentionSampleLimit, 3000);
    _contentionDurationThreshold = GetEnvironmentValue(EnvironmentVariables::ContentionDurationThreshold, 100);
    _cpuWallTimeSamplingRate = ExtractCpuWallTimeSamplingRate();
    _walltimeThreadsThreshold = ExtractWallTimeThreadsThreshold();
    _cpuThreadsThreshold = ExtractCpuThreadsThreshold();
    _codeHotspotsThreadsThreshold = ExtractCodeHotspotsThreadsThreshold();
    _minimumCores = GetEnvironmentValue<double>(EnvironmentVariables::CoreMinimumOverride, 1.0);
    _namedPipeName = GetEnvironmentValue(EnvironmentVariables::NamedPipeName, DefaultEmptyString);
    _isTimestampsAsLabelEnabled = GetEnvironmentValue(EnvironmentVariables::TimestampsAsLabelEnabled, true);
    _isAllocationRecorderEnabled = GetEnvironmentValue(EnvironmentVariables::AllocationRecorderEnabled, false);
    _isDebugInfoEnabled = GetEnvironmentValue(EnvironmentVariables::DebugInfoEnabled, false);
    _isGcThreadsCpuTimeEnabled = GetEnvironmentValue(EnvironmentVariables::GcThreadsCpuTimeEnabled,
                                  GetEnvironmentValue(EnvironmentVariables::GcThreadsCpuTimeInternalEnabled, true), true);
    _isThreadLifetimeEnabled = GetEnvironmentValue(EnvironmentVariables::ThreadLifetimeEnabled,
                                GetEnvironmentValue(EnvironmentVariables::ThreadLifetimeInternalEnabled, true), true);
    _gitRepositoryUrl = GetEnvironmentValue(EnvironmentVariables::GitRepositoryUrl, DefaultEmptyString);
    _gitCommitSha = GetEnvironmentValue(EnvironmentVariables::GitCommitSha, DefaultEmptyString);
    _isInternalMetricsEnabled = GetEnvironmentValue(EnvironmentVariables::InternalMetricsEnabled, false);
    _isSystemCallsShieldEnabled = GetEnvironmentValue(EnvironmentVariables::SystemCallsShieldEnabled, true);

    // Check CI Visibility mode
    _isCIVisibilityEnabled = GetEnvironmentValue(EnvironmentVariables::CIVisibilityEnabled, false);
    _internalCIVisibilitySpanId = uint64_t{0};
    _cpuProfilingInterval = ExtractCpuProfilingInterval();
    if (_isCIVisibilityEnabled)
    {
        // We cannot write 0ull instead of std::uint64_t{0} because on Windows, compiling in x64, std::uint64_t == unsigned long long.
        // But on Linux, it's std::uint64_t == unsigned long (=> 0ul)and it fails to compile.
        // Here we create a 0 value of type std::uint64_t which will succeed the compilation
        _internalCIVisibilitySpanId = GetEnvironmentValue(EnvironmentVariables::InternalCIVisibilitySpanId, uint64_t{0});

        // If we detect CI Visibility we allow to reduce the minimum ms in sampling rate down to 1ms.
        _cpuWallTimeSamplingRate = ExtractCpuWallTimeSamplingRate(1);
        // for timer_create based profiling
        _cpuProfilingInterval = ExtractCpuProfilingInterval(1ms);
    }

    _isEtwEnabled = GetEnvironmentValue(EnvironmentVariables::EtwEnabled, true, true);
    _isManagedActivationEnabled = GetEnvironmentValue(EnvironmentVariables::ManagedActivationEnabled, true, true);

    // check that the env var exists (special converter) and log the resulting value
    _deploymentMode = GetEnvironmentValue(EnvironmentVariables::SsiDeployed, DeploymentMode::Manual, true);
    _isEtwLoggingEnabled = GetEnvironmentValue(EnvironmentVariables::EtwLoggingEnabled, false);
    _etwReplayEndpoint = GetEnvironmentValue(EnvironmentVariables::EtwReplayEndpoint, DefaultEmptyString);
    _enablementStatus = ExtractEnablementStatus();
    _ssiLongLivedThreshold = ExtractSsiLongLivedThreshold();
    _isHttpProfilingEnabled =
        GetEnvironmentValue(
            EnvironmentVariables::HttpProfilingEnabled,
            GetEnvironmentValue(EnvironmentVariables::HttpProfilingInternalEnabled, false // support previous internal env var
            )
        );
    _httpRequestDurationThreshold = ExtractHttpRequestDurationThreshold();
    _forceHttpSampling = GetEnvironmentValue(EnvironmentVariables::ForceHttpSampling, false);
    _cpuProfilerType = GetEnvironmentValue(EnvironmentVariables::CpuProfilerType, DefaultCpuProfilerType);
    _isWaitHandleProfilingEnabled = GetEnvironmentValue(EnvironmentVariables::WaitHandleProfilingEnabled, false);
    _isHeapSnapshotEnabled = GetEnvironmentValue(EnvironmentVariables::HeapSnapshotEnabled, false);
    _heapSnapshotInterval = ExtractHeapSnapshotInterval();
    _heapSnapshotCheckInterval = ExtractHeapSnapshotCheckInterval();
    _heapSnapshotMemoryPressureThreshold = GetEnvironmentValue(EnvironmentVariables::HeapSnapshotMemoryPressureThreshold, 85);
    _heapHandleLimit = ExtractHeapHandleLimit();
    bool defaultUseManagedCodeCache =
    #if ARM64
        true;
    #else
        false;
    #endif
    _useManagedCodeCache = GetEnvironmentValue(EnvironmentVariables::UseManagedCodeCache, defaultUseManagedCodeCache);
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

int32_t Configuration::ExceptionSampleLimit() const
{
    return _exceptionSampleLimit;
}

bool Configuration::IsAllocationProfilingEnabled() const
{
    return _isAllocationProfilingEnabled;
}

int32_t Configuration::AllocationSampleLimit() const
{
    return _allocationSampleLimit;
}

bool Configuration::IsContentionProfilingEnabled() const
{
    return _isContentionProfilingEnabled;
}

bool Configuration::IsGarbageCollectionProfilingEnabled() const
{
    return _isGarbageCollectionProfilingEnabled;
}

bool Configuration::IsGcThreadsCpuTimeEnabled() const
{
    return _isGcThreadsCpuTimeEnabled;
}

bool Configuration::IsHeapProfilingEnabled() const
{
    return _isHeapProfilingEnabled;
}

bool Configuration::IsThreadLifetimeEnabled() const
{
    return _isThreadLifetimeEnabled;
}

int32_t Configuration::ContentionSampleLimit() const
{
    return _contentionSampleLimit;
}

int32_t Configuration::ContentionDurationThreshold() const
{
    return _contentionDurationThreshold;
}

std::chrono::nanoseconds Configuration::CpuWallTimeSamplingRate() const
{
    return _cpuWallTimeSamplingRate;
}

int32_t Configuration::WalltimeThreadsThreshold() const
{
    return _walltimeThreadsThreshold;
}

int32_t Configuration::CpuThreadsThreshold() const
{
    return _cpuThreadsThreshold;
}

int32_t Configuration::CodeHotspotsThreadsThreshold() const
{
    return _codeHotspotsThreadsThreshold;
}

double Configuration::MinimumCores() const
{
    return _minimumCores;
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

int32_t Configuration::GetAgentPort() const
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

bool Configuration::UseManagedCodeCache() const
{
    return _useManagedCodeCache;
}

bool Configuration::IsAllocationRecorderEnabled() const
{
    return _isAllocationRecorderEnabled;
}

bool Configuration::IsInternalMetricsEnabled() const
{
    return _isInternalMetricsEnabled;
}

bool Configuration::IsCIVisibilityEnabled() const
{
    return _isCIVisibilityEnabled;
}

std::uint64_t Configuration::GetCIVisibilitySpanId() const
{
    return _internalCIVisibilitySpanId;
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
    return baseDirectory / WStr(R"(Datadog .NET Tracer\logs)");
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

const std::string& Configuration::GetNamedPipeName() const
{
    return _namedPipeName;
}

bool Configuration::IsTimestampsAsLabelEnabled() const
{
    return _isTimestampsAsLabelEnabled;
}

std::string const& Configuration::GetGitRepositoryUrl() const
{
    return _gitRepositoryUrl;
}

std::string const& Configuration::GetGitCommitSha() const
{
    return _gitCommitSha;
}

//
// shared::TryParse does not work on Linux
// not found the issue yet.
// For now, replace shared::TryParse by this function
// TODO Once in the Tracer repo:
// - replace shared::TryParse by this implementation
// - add tests

static bool TryParse(shared::WSTRING const& s, int32_t& result)
{
    auto r = shared::Trim(s);
    if (r.empty())
    {
        result = 0;
        return false;
    }

    try
    {
        result = std::stoi(shared::ToString(r));
        return true;
    }
    catch (std::exception const&)
    {
        // TODO log
    }
    result = 0;
    return false;
}

static bool TryParse(shared::WSTRING const& s, uint64_t& result)
{
    auto r = shared::Trim(s);

    if (r.empty())
    {
        result = 0;
        return false;
    }

    try
    {
        auto str = shared::ToString(r);
        result = std::stoull(str);
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
    int32_t interval;
    if (TryParse(r, interval))
    {
        return std::chrono::seconds(interval);
    }

    return GetDefaultUploadInterval();
}

std::chrono::milliseconds Configuration::ExtractCpuProfilingInterval(std::chrono::milliseconds minimum)
{
    // For normal path (no CI-visibility), 9ms is the default and lowest value we can have
    // for CI-Visibility we allow it to be less
    auto interval = GetEnvironmentValue(EnvironmentVariables::CpuProfilingInterval, DefaultCpuProfilingInterval);
    return std::max(interval, minimum);
}

std::chrono::nanoseconds Configuration::ExtractCpuWallTimeSamplingRate(int minimum)
{
    // default sampling rate is 9 ms; could be changed via env vars but down to a minimum of 5 ms
    int64_t rate = std::max(GetEnvironmentValue(EnvironmentVariables::CpuWallTimeSamplingRate, 9), minimum);
    rate *= 1000000;
    return std::chrono::nanoseconds(rate);
}

int32_t Configuration::ExtractWallTimeThreadsThreshold()
{
    // default threads to sample for wall time is 5; could be changed via env vars from 5 to 64
    int32_t threshold =
        std::min(
            std::max(GetEnvironmentValue(EnvironmentVariables::WalltimeThreadsThreshold, 5), 5),
            64);
    return threshold;
}

int32_t Configuration::ExtractCodeHotspotsThreadsThreshold()
{
    // default threads to sample for codehotspots is 10; could be changed via env vars but down to 1ms
    int32_t threshold = std::max(GetEnvironmentValue(EnvironmentVariables::CodeHotspotsThreadsThreshold, 10), 1);
    return threshold;
}

int32_t Configuration::ExtractCpuThreadsThreshold()
{
    // default threads to sample for CPU profiling is 64; could be changed via env vars from 5 to 128
    int32_t threshold =
        std::min(
            std::max(GetEnvironmentValue(EnvironmentVariables::CpuTimeThreadsThreshold, 64), 5),
            128);
    return threshold;
}

bool Configuration::GetContention()
{
    // enabled by default
    bool lockContentionEnabled = true;

    // first look at the supported env var
    if (IsEnvironmentValueSet(EnvironmentVariables::LockContentionProfilingEnabled, lockContentionEnabled))
    {
        Log::Info("Configuration: ", EnvironmentVariables::LockContentionProfilingEnabled, " env var is set - lock contention is enabled");
        return lockContentionEnabled;
    }

    // if not there, look at the deprecated one
    return GetEnvironmentValue(EnvironmentVariables::DeprecatedContentionProfilingEnabled, lockContentionEnabled, true);
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

bool Configuration::IsDebugInfoEnabled() const
{
    return _isDebugInfoEnabled;
}

bool Configuration::IsSystemCallsShieldEnabled() const
{
#ifdef LINUX
    return _isSystemCallsShieldEnabled;
#else
    return false;
#endif
}

bool Configuration::IsEtwEnabled() const
{
#ifdef LINUX
    return false;
#else
    return _isEtwEnabled;
#endif
}

bool Configuration::IsEtwLoggingEnabled() const
{
#ifdef LINUX
    return false;
#else
    return _isEtwLoggingEnabled;
#endif
}

std::string const& Configuration::GetEtwReplayEndpoint() const
{
#ifdef LINUX
    return DefaultEmptyString;
#else
    return _etwReplayEndpoint;
#endif
}

EnablementStatus Configuration::GetEnablementStatus() const
{
    return _enablementStatus;
}

DeploymentMode Configuration::GetDeploymentMode() const
{
    return _deploymentMode;
}

std::chrono::milliseconds Configuration::GetSsiLongLivedThreshold() const
{
    return _ssiLongLivedThreshold;
}

CpuProfilerType Configuration::GetCpuProfilerType() const
{
    return _cpuProfilerType;
}

std::chrono::milliseconds Configuration::GetCpuProfilingInterval() const
{
    return _cpuProfilingInterval;
}

static bool convert_to(shared::WSTRING const& s, bool& result)
{
    return shared::TryParseBooleanEnvironmentValue(s, result);
}

static bool convert_to(shared::WSTRING const& s, std::string& result)
{
    result = shared::ToString(shared::Trim(s));
    return true;
}

static bool convert_to(shared::WSTRING const& s, shared::WSTRING& result)
{
    result = shared::Trim(s);
    return true;
}

static bool convert_to(shared::WSTRING const& s, int32_t& result)
{
    return TryParse(s, result);
}

static bool convert_to(shared::WSTRING const& s, uint64_t& result)
{
    return TryParse(s, result);
}

static bool convert_to(shared::WSTRING const& s, double& result)
{
    auto str = shared::ToString(shared::Trim(s));

    char* endPtr = nullptr;
    const char* ptr = str.c_str();
    result = strtod(ptr, &endPtr);

    // non numeric input
    if (ptr == endPtr)
    {
        return false;
    }

    // Based on tests, numbers such as "0.1.2" are converted into 0.1 without error
    return (errno != ERANGE);
}

static bool convert_to(shared::WSTRING const& s, std::chrono::milliseconds& result)
{
    std::uint64_t value;
    auto parsed = TryParse(s, value);
    if (parsed)
    {
        result = std::chrono::milliseconds(value);
    }
    return parsed;
}

static bool convert_to(shared::WSTRING const& s, DeploymentMode& result)
{
    // if we reach here it means the env var exists
    result = DeploymentMode::SingleStepInstrumentation;
    return true;
}


template <typename T>
T Configuration::GetEnvironmentValue(shared::WSTRING const& name, T const& defaultValue, bool shouldLog)
{
    if (!shared::EnvironmentExist(name))
    {
        if (shouldLog)
        {
            Log::Info("Configuration: ", name, " env var is not set - '", defaultValue, "' is used as default value");
        }
        return defaultValue;
    }

    T result{};
    auto r = shared::GetEnvironmentValue(name);
    if (!convert_to(r, result))
    {
        if (shouldLog)
        {
            Log::Info("Configuration: ", name, " env var is set to '", r,"' that cannot be converted - '", defaultValue, "' is used as default value");
        }
        return defaultValue;
    }

    if (shouldLog)
    {
        Log::Info("Configuration: ", name, " env var is set to '", r, "'");
    }
    return result;
}

template <typename T>
bool Configuration::IsEnvironmentValueSet(shared::WSTRING const& name, T& value)
{
    auto r = shared::Trim(shared::GetEnvironmentValue(name));
    if (r.empty()) return false;

    T result{};
    if (!convert_to(r, result)) return false;

    value = result;
    return true;
}

EnablementStatus Configuration::ExtractEnablementStatus()
{
    // wait for the managed layer to set the activation status
    if (_isManagedActivationEnabled)
    {
        return EnablementStatus::Standby;
    }

    // kill switch for local environment variables
    if (shared::EnvironmentExist(EnvironmentVariables::ProfilerEnabled))
    {
        auto isEnabled = false;
        auto enabled = shared::GetEnvironmentValue(EnvironmentVariables::ProfilerEnabled);
        auto parsed = shared::TryParseBooleanEnvironmentValue(enabled, isEnabled);

        if (parsed)
        {
            return isEnabled
                ? EnablementStatus::ManuallyEnabled
                : EnablementStatus::ManuallyDisabled;
        }

        // It is possible that a Single Step Instrumentation deployment was done
        // and the profiler was enabled during that step. In that case, the "auto" value
        // will be set and profiler should be enabled.
        return !enabled.empty() && enabled == WStr("auto")
            ? EnablementStatus::Auto
            : EnablementStatus::ManuallyDisabled;
    }

    // Note: the initial support of adding "profiler" in EnvironmentVariables::SsiDeployed
    // has never been implemented, so we are not checking for it here.
    return EnablementStatus::NotSet;
}

std::chrono::milliseconds Configuration::ExtractSsiLongLivedThreshold() const
{
    auto const defaultValue = 30'000ms;
    auto value = GetEnvironmentValue(EnvironmentVariables::SsiLongLivedThreshold, defaultValue);

    if (value < 0ms)
        return defaultValue;
    return std::chrono::milliseconds(value);
}

bool Configuration::IsHttpProfilingEnabled() const
{
    return _isHttpProfilingEnabled;
}

bool Configuration::ForceHttpSampling() const
{
    return _forceHttpSampling;
}

bool Configuration::IsWaitHandleProfilingEnabled() const
{
    return _isWaitHandleProfilingEnabled;
}

bool Configuration::IsManagedActivationEnabled() const
{
    return _isManagedActivationEnabled;
}

void Configuration::SetEnablementStatus(EnablementStatus status)
{
    _enablementStatus = status;
}

std::chrono::milliseconds Configuration::ExtractHttpRequestDurationThreshold() const
{
    auto const defaultValue = 50ms;
    auto value = GetEnvironmentValue(EnvironmentVariables::HttpRequestDurationThreshold, defaultValue);
    if (value < 0ms)
        return defaultValue;

    return std::chrono::milliseconds(value);
}

std::chrono::milliseconds Configuration::GetHttpRequestDurationThreshold() const
{
    return _httpRequestDurationThreshold;
}

bool Configuration::IsHeapSnapshotEnabled() const
{
    return _isHeapSnapshotEnabled;
}

std::chrono::minutes Configuration::GetDefaultHeapSnapshotInterval() const
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::DevelopmentConfiguration);

    bool isDev;
    if (shared::TryParseBooleanEnvironmentValue(r, isDev) && isDev)
        return DefaultDevHeapSnapshotInterval;

    return DefaultProdHeapSnapshotInterval;
}

std::chrono::minutes Configuration::ExtractHeapSnapshotInterval() const
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::HeapSnapshotInterval);
    int32_t interval;
    if (TryParse(r, interval))
    {
        return std::chrono::minutes(interval);
    }

    return GetDefaultHeapSnapshotInterval();
}

std::chrono::minutes Configuration::GetHeapSnapshotInterval() const
{
    return _heapSnapshotInterval;
}

std::chrono::milliseconds Configuration::ExtractHeapSnapshotCheckInterval() const
{
    auto r = shared::GetEnvironmentValue(EnvironmentVariables::HeapSnapshotCheckInterval);
    int32_t interval;
    if (TryParse(r, interval))
    {
        return std::chrono::milliseconds(interval);
    }

    return 250ms;
}

std::chrono::milliseconds Configuration::GetHeapSnapshotCheckInterval() const
{
    return _heapSnapshotCheckInterval;
}

uint32_t Configuration::GetHeapSnapshotMemoryPressureThreshold() const
{
    return _heapSnapshotMemoryPressureThreshold;
}

int32_t Configuration::ExtractHeapHandleLimit() const
{
    // default handle count limit is 4096; could be changed via env vars from 1024 to 16000
    int32_t limit =
        std::min(
            std::max(GetEnvironmentValue(EnvironmentVariables::HeapHandleLimit, 4096), 1024),
            16000);
    return limit;
}

uint32_t Configuration::GetHeapHandleLimit() const
{
    return _heapHandleLimit;
}
