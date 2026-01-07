#pragma once
#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "cor.h"
#include "corprof.h"

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

#include "Configuration.h"
#include "IAllocationsListener.h"
#include "IApplicationStore.h"
#include "IContentionListener.h"
#include "IExporter.h"
#include "IMetricsSender.h"
#include "IRuntimeIdStore.h"
#include "ISamplesCollector.h"
#include "ISamplesProvider.h"
#include "CpuProfilerType.h"
#include "ISsiLifetime.h"
#include "ISsiManager.h"
#include "Sample.h"
#include "SamplesEnumerator.h"
#include "TagsHelper.h"

#include <chrono>
#include <memory>

namespace libdatadog {
    class SymbolsStore;
}

extern "C" {
    #include "datadog/profiling.h"
    #include "datadog/common.h"
}

class MockConfiguration : public IConfiguration
{
public:
    ~MockConfiguration() override = default;
    MOCK_METHOD(bool, IsDebugLogEnabled, (), (const override));
    MOCK_METHOD(fs::path const&, GetLogDirectory, (), (const override));
    MOCK_METHOD(fs::path const&, GetProfilesOutputDirectory, (), (const override));
    MOCK_METHOD(bool, IsNativeFramesEnabled, (), (const override));
    MOCK_METHOD(bool, IsOperationalMetricsEnabled, (), (const override));
    MOCK_METHOD(std::chrono::seconds, GetUploadInterval, (), (const override));
    MOCK_METHOD(tags const&, GetUserTags, (), (const override));
    MOCK_METHOD(std::string const&, GetVersion, (), (const override));     // return DD_VERSION, Unspecified-Version if not specified
    MOCK_METHOD(std::string const&, GetEnvironment, (), (const override)); // return DD_ENV, Unspecified-Env if not specified
    MOCK_METHOD(std::string const&, GetHostname, (), (const override));    // return the machine hostname
    MOCK_METHOD(std::string const&, GetAgentUrl, (), (const override));
    MOCK_METHOD(std::string const&, GetAgentHost, (), (const override));
    MOCK_METHOD(int, GetAgentPort, (), (const override));
    MOCK_METHOD(std::string const&, GetSite, (), (const override));
    MOCK_METHOD(std::string const&, GetApiKey, (), (const override));
    MOCK_METHOD(std::string const&, GetServiceName, (), (const override));
    MOCK_METHOD(bool, IsFFLibddprofEnabled, (), (const override));
    MOCK_METHOD(bool, IsAgentless, (), (const override));
    MOCK_METHOD(bool, IsWallTimeProfilingEnabled, (), (const override));
    MOCK_METHOD(bool, IsCpuProfilingEnabled, (), (const override));
    MOCK_METHOD(bool, IsExceptionProfilingEnabled, (), (const override));
    MOCK_METHOD(int, ExceptionSampleLimit, (), (const override));
    MOCK_METHOD(bool, IsAllocationProfilingEnabled, (), (const override));
    MOCK_METHOD(bool, IsContentionProfilingEnabled, (), (const override));
    MOCK_METHOD(double, MinimumCores, (), (const override));
    MOCK_METHOD(int32_t, AllocationSampleLimit, (), (const override));
    MOCK_METHOD(int32_t, ContentionSampleLimit, (), (const override));
    MOCK_METHOD(int32_t, ContentionDurationThreshold, (), (const override));
    MOCK_METHOD(std::chrono::nanoseconds, CpuWallTimeSamplingRate, (), (const override));
    MOCK_METHOD(std::string const&, GetNamedPipeName, (), (const override));
    MOCK_METHOD(bool, IsTimestampsAsLabelEnabled, (), (const override));
    MOCK_METHOD(int32_t, WalltimeThreadsThreshold, (), (const override));
    MOCK_METHOD(int32_t, CpuThreadsThreshold, (), (const override));
    MOCK_METHOD(int32_t, CodeHotspotsThreadsThreshold, (), (const override));
    MOCK_METHOD(bool, IsGarbageCollectionProfilingEnabled, (), (const override));
    MOCK_METHOD(bool, IsHeapProfilingEnabled, (), (const override));
    MOCK_METHOD(bool, UseBacktrace2, (), (const override));
    MOCK_METHOD(bool, IsAllocationRecorderEnabled, (), (const override));
    MOCK_METHOD(bool, IsDebugInfoEnabled, (), (const override));
    MOCK_METHOD(bool, IsGcThreadsCpuTimeEnabled, (), (const override));
    MOCK_METHOD(bool, IsThreadLifetimeEnabled, (), (const override));
    MOCK_METHOD(std::string const&, GetGitRepositoryUrl, (), (const override));
    MOCK_METHOD(std::string const&, GetGitCommitSha, (), (const override));
    MOCK_METHOD(bool, IsInternalMetricsEnabled, (), (const override));
    MOCK_METHOD(bool, IsSystemCallsShieldEnabled, (), (const override));
    MOCK_METHOD(bool, IsCIVisibilityEnabled, (), (const override));
    MOCK_METHOD(std::uint64_t, GetCIVisibilitySpanId, (), (const override));
    MOCK_METHOD(bool, IsEtwEnabled, (), (const override));
    MOCK_METHOD(EnablementStatus, GetEnablementStatus, (), (const override));
    MOCK_METHOD(DeploymentMode, GetDeploymentMode, (), (const override));
    MOCK_METHOD(bool, IsEtwLoggingEnabled, (), (const override));
    MOCK_METHOD(std::string const&, GetEtwReplayEndpoint, (), (const override));
    MOCK_METHOD(CpuProfilerType, GetCpuProfilerType, (), (const override));
    MOCK_METHOD(std::chrono::milliseconds, GetCpuProfilingInterval, (), (const override));
    MOCK_METHOD(std::chrono::milliseconds, GetSsiLongLivedThreshold, (), (const override));
    MOCK_METHOD(bool, IsTelemetryToDiskEnabled, (), (const override));
    MOCK_METHOD(bool, IsSsiTelemetryEnabled, (), (const override));
    MOCK_METHOD(bool, IsHttpProfilingEnabled, (), (const override));
    MOCK_METHOD(std::chrono::milliseconds, GetHttpRequestDurationThreshold, (), (const override));
    MOCK_METHOD(bool, ForceHttpSampling, (), (const override));
    MOCK_METHOD(bool, IsWaitHandleProfilingEnabled, (), (const override));
    MOCK_METHOD(bool, IsManagedActivationEnabled, (), (const override));
    MOCK_METHOD(void, SetEnablementStatus, (EnablementStatus status), (override));
};

class MockExporter : public IExporter
{
public:
    MOCK_METHOD(void, Add, (std::shared_ptr<Sample> const& sample), (override));
    MOCK_METHOD(bool, Export, (bool lastCall), (override));
    MOCK_METHOD(void, SetEndpoint, (const std::string& runtimeId, uint64_t traceId, const std::string& endpoint), (override));
    MOCK_METHOD(void, RegisterUpscaleProvider, (IUpscaleProvider * provider), (override));
    MOCK_METHOD(void, RegisterUpscalePoissonProvider, (IUpscalePoissonProvider * provider), (override));
    MOCK_METHOD(void, RegisterProcessSamplesProvider, (ISamplesProvider * provider), (override));
    MOCK_METHOD(void, RegisterApplication, (std::string_view runtimeId), (override));
};

class MockSamplesCollector : public ISamplesCollector
{
public:
    MOCK_METHOD(void, Register, (ISamplesProvider* sampleProvider), (override));
    MOCK_METHOD(void, RegisterBatchedProvider, (IBatchedSamplesProvider* sampleProvider), (override));
};

class MockSampleProvider : public ISamplesProvider
{
public:
    MOCK_METHOD(std::unique_ptr<SamplesEnumerator>, GetSamples, (), (override));
    MOCK_METHOD(const char*, GetName, (), (override));
};

class MockSsiManager : public ISsiManager
{
public:
    // Inherited via ISsiManager
    MOCK_METHOD(void, OnStableConfiguration, (), (override));
    MOCK_METHOD(void, OnSpanCreated, (), (override));
    MOCK_METHOD(bool, IsSpanCreated, (),  (const override));
    MOCK_METHOD(bool, IsLongLived, (), (const override));
    MOCK_METHOD(bool, IsProfilerEnabled, (), (override));
    MOCK_METHOD(bool, IsProfilerStarted, (), (override));
    MOCK_METHOD(void, ProcessStart, (), (override));
    MOCK_METHOD(void, ProcessEnd, (), (override));
    MOCK_METHOD(SkipProfileHeuristicType, GetSkipProfileHeuristic, (), (const override));
    MOCK_METHOD(DeploymentMode, GetDeploymentMode, (), (const override));
};

class MockMetricsSender : public IMetricsSender
{
public:
    ~MockMetricsSender() override = default;

    bool Gauge(std::string const& name, double value) override
    {
        ++_nbCallsToGauge;
        return true;
    }
    bool Counter(const std::string& name, std::uint64_t value, const Tags& additionalTags = {}) override
    {
        ++_nbCallsToCounter;
        return true;
    }

    bool WasCounterCalled() const
    {
        return _nbCallsToCounter > 0;
    }

    bool WasGaugerCalled() const
    {
        return _nbCallsToGauge > 0;
    }

private:
    std::uint32_t _nbCallsToCounter;
    std::uint32_t _nbCallsToGauge;
};

class MockApplicationStore : public IApplicationStore
{
public:
    MOCK_METHOD(ApplicationInfo, GetApplicationInfo, (const std::string& runtimeId), (override));
    MOCK_METHOD(void, SetApplicationInfo, (const std::string&, const std::string&, const std::string&, const std::string&), (override));
    MOCK_METHOD(void, SetGitMetadata, (std::string, std::string, std::string), (override));
};

class MockRuntimeIdStore : public IRuntimeIdStore
{
public:
    MOCK_METHOD(const char*, GetId, (AppDomainID appDomainId), (override));
};

class MockProcessSamplesProvider : public ISamplesProvider
{
public:
    MOCK_METHOD(std::unique_ptr<SamplesEnumerator>, GetSamples, (), (override));
    MOCK_METHOD(const char*, GetName, (), (override));
};

class SsiLifetimeForTest : public ISsiLifetime
{
public:
    void OnStartDelayedProfiling() override
    {
        _hasBeenStarted = true;
    }

    bool IsProfilingEnabled() const
    {
        return _hasBeenStarted;
    }

private:
    bool _hasBeenStarted = false;
};

class MockContentionListener : public IContentionListener
{
public:
    MOCK_METHOD(void, OnContention, (std::chrono::nanoseconds contentionDurationNs), (override));
    MOCK_METHOD(void, OnContention, (std::chrono::nanoseconds timestamp, uint32_t threadId, std::chrono::nanoseconds contentionDurationNs, const std::vector<uintptr_t>& stack), (override));
    MOCK_METHOD(void, SetBlockingThread, (uint64_t osThreadId), (override));
    MOCK_METHOD(void, OnWaitStart, (std::chrono::nanoseconds timestamp, uintptr_t associatedObjectId), (override));
    MOCK_METHOD(void, OnWaitStop, (std::chrono::nanoseconds timestamp), (override));
};

class MockAllocationListener : public IAllocationsListener
{
public:
    MOCK_METHOD(void, OnAllocation, (uint32_t allocationKind,
                              ClassID classId,
                              const WCHAR* typeName,
                              uintptr_t address,
                              uint64_t objectSize,
                              uint64_t allocationAmount), (override));

    // for .NET Framework, events are received asynchronously
    // and the callstack is received as a sibling event
    // --> we cannot walk the stack of the current thread
    MOCK_METHOD(void, OnAllocation, (std::chrono::nanoseconds timestamp,
                              uint32_t threadId,
                              uint32_t allocationKind,
                              ClassID classId,
                              const std::string& typeName,
                              uint64_t allocationAmount,
                              const std::vector<uintptr_t>& stack), (override));

    MOCK_METHOD(void, OnAllocationSampled, (uint32_t allocationKind,
                              ClassID classId,
                              const WCHAR* typeName,
                              uintptr_t address,
                              uint64_t objectSize,
                              uint64_t allocationByteOffset), (override));
};

template <typename T, typename U, typename... Args>
std::pair<std::unique_ptr<T>, U&> CreateMockForUniquePtr(Args... args)
{
    std::unique_ptr<T> interf = std::make_unique<U>(args...);
    auto mock = static_cast<U*>(interf.get());
    return {std::move(interf), *mock};
}

std::tuple<std::unique_ptr<IConfiguration>, MockConfiguration&> CreateConfiguration();

std::tuple<std::shared_ptr<ISamplesProvider>, MockSampleProvider&> CreateSamplesProvider();

std::tuple<std::unique_ptr<IExporter>, MockExporter&> CreateExporter();
std::tuple<std::unique_ptr<ISamplesCollector>, MockSamplesCollector&> CreateSamplesCollector();
std::tuple<std::unique_ptr<ISsiManager>, MockSsiManager&> CreateSsiManager();

std::shared_ptr<Sample> CreateSample(libdatadog::SymbolsStore* symbolsStore, std::string_view runtimeId, const std::vector<std::pair<ddog_prof_FunctionId2, ddog_prof_MappingId2>>& callstack, const std::vector<std::pair<ddog_prof_StringId2, std::string>>& labels, std::int64_t value);

std::vector<std::pair<ddog_prof_FunctionId2, ddog_prof_MappingId2>> CreateCallstack(int depth, libdatadog::SymbolsStore* symbolsStore);