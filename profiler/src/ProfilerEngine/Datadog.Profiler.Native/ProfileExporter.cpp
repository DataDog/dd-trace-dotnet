// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfileExporter.h"

#include "AgentHttpExporter.h"
#include "EncodedPprof.h"
#include "FileHelper.h"
#include "IAllocationsRecorder.h"
#include "IApplicationStore.h"
#include "IEnabledProfilers.h"
#include "IMetadataProvider.h"
#include "IMetricsSender.h"
#include "IRuntimeInfo.h"
#include "ISamplesProvider.h"
#include "ISsiManager.h"
#include "IUpscaleProvider.h"
#include "Log.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"
#include "PprofBuilder.h"
#include "Sample.h"
#include "SamplesEnumerator.h"
#include "ScopeFinalizer.h"
#include "dd_profiler_version.h"
#include "IHeapSnapshotManager.h"
#include "IGcSettingsProvider.h"
#include "MetadataProvider.h"

#include <cassert>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <string.h>
#include <time.h>

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

#define BUFFER_MAX_SIZE 512

tags ProfileExporter::CommonTags = {
    {"language", "dotnet"},
    {"profiler_version", PROFILER_VERSION},
#ifdef BIT64
    {"process_architecture", "x64"},
#else
    {"process_architecture", "x86"}
#endif
};

// need to be static so it leave longer for the shared library
std::string const ProfileExporter::ProcessId = std::to_string(OpSysTools::GetProcId());

int32_t const ProfileExporter::RequestTimeOutMs = 10000;

std::string const ProfileExporter::LibraryName = "dd-profiling-dotnet";

std::string const ProfileExporter::LibraryVersion = PROFILER_VERSION;

std::string const ProfileExporter::LanguageFamily = "dotnet";

std::string const ProfileExporter::RequestFileName = "auto.pprof";

std::string const ProfileExporter::ProfilePeriodType = "RealTime";

std::string const ProfileExporter::ProfilePeriodUnit = "Nanoseconds";

std::string const ProfileExporter::MetricsFilename = "metrics.json";

std::string const ProfileExporter::AllocationsExtension = ".balloc";

std::string const ProfileExporter::ClassHistogramFilename = "histogram.json";

std::string const ProfileExporter::StartTime = OsSpecificApi::GetProcessStartTime();

ProfileExporter::ProfileExporter(
    std::vector<SampleValueType> sampleTypeDefinitions,
    IConfiguration* configuration,
    IApplicationStore* applicationStore,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers,
    MetricsRegistry& metricsRegistry,
    IMetadataProvider* metadataProvider,
    ISsiManager* ssiManager,
    IAllocationsRecorder* allocationsRecorder,
    IHeapSnapshotManager* heapSnapshotManager)
    :
    _sampleTypeDefinitions{std::move(sampleTypeDefinitions)},
    _applicationStore{applicationStore},
    _metricsRegistry{metricsRegistry},
    _allocationsRecorder{allocationsRecorder},
    _metadataProvider{metadataProvider},
    _configuration{configuration},
    _runtimeInfo{runtimeInfo},
    _ssiManager{ssiManager},
    ProviderList{GetEnabledProfilers(enabledProfilers)},
    _heapSnapshotManager{heapSnapshotManager}
{
    _exporter = CreateExporter(_configuration, CreateFixedTags(_configuration, runtimeInfo, enabledProfilers));
    _outputPath = CreatePprofOutputPath(_configuration);
    _metricsFileFolder = _configuration->GetProfilesOutputDirectory();
}

ProfileExporter::~ProfileExporter()
{
    std::lock_guard lck(_perAppInfoLock);

    for (auto& [runtimeId, appInfo] : _perAppInfo)
    {
        {
            std::lock_guard lockProfile(appInfo.lock);

            if (appInfo.profile != nullptr)
            {
                appInfo.profile.reset();
            }

            appInfo.profile = nullptr;
        }
    }
    _perAppInfo.clear();
}

std::unique_ptr<AgentHttpExporter> ProfileExporter::CreateExporter(IConfiguration* configuration, tags tags)
{
    auto url = BuildAgentEndpoint(configuration);

    std::string host;
    int port = 0;
    std::string path;
    if (!ParseHttpUrl(url, host, port, path))
    {
        Log::Error("Failed to create the exporter: unsupported agent endpoint '", url, "'. Only plain 'http://host:port' endpoints are supported.");
        return nullptr;
    }

    auto const& outputDirectory = configuration->GetProfilesOutputDirectory();

    return std::make_unique<AgentHttpExporter>(
        std::move(host), port, std::move(path),
        LibraryName, LibraryVersion, LanguageFamily,
        std::move(tags), outputDirectory, RequestTimeOutMs);
}

std::unique_ptr<PprofBuilder> ProfileExporter::CreateProfile(std::string serviceName)
{
    return PprofBuilder::Create(_configuration, _sampleTypeDefinitions, ProfilePeriodType, ProfilePeriodUnit, std::move(serviceName));
}

void ProfileExporter::RegisterUpscaleProvider(IUpscaleProvider* provider)
{
    assert(provider != nullptr);
    _upscaledProviders.push_back(provider);
}

void ProfileExporter::RegisterUpscalePoissonProvider(IUpscalePoissonProvider* provider)
{
    assert(provider != nullptr);
    _upscaledPoissonProviders.push_back(provider);
}

void ProfileExporter::RegisterProcessSamplesProvider(ISamplesProvider* provider)
{
    assert(provider != nullptr);
    _processSamplesProviders.push_back(provider);
}

void ProfileExporter::RegisterApplication(std::string_view runtimeId)
{
    GetOrCreateInfo(runtimeId);
}

void ProfileExporter::RegisterGcSettingsProvider(IGcSettingsProvider* provider)
{
    _gcSettingsProvider = provider;
}


tags ProfileExporter::CreateFixedTags(
    IConfiguration* configuration,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers)
{
    tags result;

    for (auto const& [name, value] : CommonTags)
    {
        result.emplace_back(name, value);
    }

    result.emplace_back("process_id", ProcessId);
    result.emplace_back("host", configuration->GetHostname());

    for (auto const& [name, value] : configuration->GetUserTags())
    {
        result.emplace_back(name, value);
    }

    return result;
}

std::string ProfileExporter::GetEnabledProfilers(IEnabledProfilers* enabledProfilers)
{
    const char* separator = "_"; // ',' are not allowed and +/SPACE would be transformed into '_' anyway
    std::stringstream buffer;
    bool emptyList = true;

    if (enabledProfilers->IsEnabled(RuntimeProfiler::WallTime))
    {
        buffer << "walltime";
        emptyList = false;
    }
    if (enabledProfilers->IsEnabled(RuntimeProfiler::Cpu))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "cpu";
        emptyList = false;
    }
    if (enabledProfilers->IsEnabled(RuntimeProfiler::Exceptions))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "exceptions";
        emptyList = false;
    }
    if (enabledProfilers->IsEnabled(RuntimeProfiler::Allocations))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "allocations";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::LockContention))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "lock";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::GC))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "gc";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::Heap))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "heap";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::Network))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "http";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::CpuGc))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "cpuGc";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::ThreadsLifetime))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "threadsLifetime";
        emptyList = false;
    }

    if (enabledProfilers->IsEnabled(RuntimeProfiler::HeapSnapshot))
    {
        if (!emptyList)
        {
            buffer << separator;
        }
        buffer << "heapsnapshot";
        emptyList = false;
    }

    return buffer.str();
}

std::string ProfileExporter::BuildAgentEndpoint(IConfiguration const* configuration)
{
    // This phase only supports plain TCP HTTP to the agent (no Unix domain
    // socket, no Windows named pipe, no agentless HTTPS).
    auto url = configuration->GetAgentUrl(); // copy expected here

    if (url.empty())
    {
        // Use default HTTP endpoint
        std::stringstream oss;
        oss << "http://" << configuration->GetAgentHost() << ":" << configuration->GetAgentPort();
        url = oss.str();
    }

    Log::Info("Using agent endpoint ", url);

    return url;
}

// Parse "http://host:port[/optional-path-ignored]" into host and port. The
// profiling intake path is fixed, so any path in the URL is ignored.
bool ProfileExporter::ParseHttpUrl(std::string const& url, std::string& host, int& port, std::string& path)
{
    static const std::string HttpScheme = "http://";
    static const std::string ProfilingPath = "/profiling/v1/input";

    path = ProfilingPath;

    if (url.rfind(HttpScheme, 0) != 0)
    {
        return false;
    }

    auto authority = url.substr(HttpScheme.size());

    // strip any path component (we always POST to the fixed profiling path)
    auto slashPos = authority.find('/');
    if (slashPos != std::string::npos)
    {
        authority = authority.substr(0, slashPos);
    }

    if (authority.empty())
    {
        return false;
    }

    auto colonPos = authority.rfind(':');
    if (colonPos == std::string::npos)
    {
        host = authority;
        port = 80;
        return !host.empty();
    }

    host = authority.substr(0, colonPos);
    auto portStr = authority.substr(colonPos + 1);
    if (host.empty() || portStr.empty())
    {
        return false;
    }

    try
    {
        port = std::stoi(portStr);
    }
    catch (...)
    {
        return false;
    }

    return port > 0;
}

ProfileExporter::ProfileInfoScope ProfileExporter::GetOrCreateInfo(std::string_view runtimeId)
{
    std::lock_guard lock(_perAppInfoLock);

    auto& profileInfo = _perAppInfo[runtimeId];

    return profileInfo;
}

void ProfileExporter::Add(PprofBuilder* profile, std::shared_ptr<Sample> const& sample)
{
    auto success = profile->Add(sample);
    if (!success)
    {
        static bool firstTimeError = true;
        if (firstTimeError)
        {
            Log::Error("Failed to add a sample: ", success.message());

            firstTimeError = false;
        }
    }
}

void ProfileExporter::Add(std::shared_ptr<Sample> const& sample)
{
    auto profileInfoScope = GetOrCreateInfo(sample->GetRuntimeId());

    if (profileInfoScope.profileInfo.profile == nullptr)
    {
        auto applicationInfo = _applicationStore->GetApplicationInfo(std::string(sample->GetRuntimeId()));
        profileInfoScope.profileInfo.profile = CreateProfile(applicationInfo.ServiceName);
        if (profileInfoScope.profileInfo.profile == nullptr)
        {
            Log::Error("Failed to create profile for service '", applicationInfo.ServiceName,
                       "' (runtime id: ", sample->GetRuntimeId(),
                       ", sample type definitions: ", _sampleTypeDefinitions.size(),
                       "). Sample will be dropped.");
            return;
        }
    }
    auto* profile = profileInfoScope.profileInfo.profile.get();
    Add(profile, sample);
    profileInfoScope.profileInfo.samplesCount++;
}

std::optional<ProfileExporter::ProfileInfoScope> ProfileExporter::GetInfo(const std::string& runtimeId)
{
    std::lock_guard lock(_perAppInfoLock);

    // since the runtime id lifetime does not extend this method call, we can't use it as a key
    // (i.e. the string_view would point to a long gone temporary string)
    auto it = _perAppInfo.find(runtimeId);
    if (it == _perAppInfo.end())
    {
        return {};
    }

    // The line below will implicit create a ProfileInfoScope from the ProfileInfo
    return it->second;
}

void ProfileExporter::SetEndpoint(const std::string& runtimeId, uint64_t traceId, const std::string& endpoint)
{
    auto scope = GetInfo(runtimeId);

    if (!scope.has_value())
    {
        return;
    }

    auto& profileInfoScope = scope.value();

    if (profileInfoScope.profileInfo.profile == nullptr)
    {
        auto applicationInfo = _applicationStore->GetApplicationInfo(runtimeId);
        profileInfoScope.profileInfo.profile = CreateProfile(applicationInfo.ServiceName);
        if (profileInfoScope.profileInfo.profile == nullptr)
        {
            Log::Error("Failed to create profile for service '", applicationInfo.ServiceName,
                       "' (runtime id: ", runtimeId,
                       ", sample type definitions: ", _sampleTypeDefinitions.size(),
                       ") while setting endpoint. Endpoint will not be set.");
            return;
        }
    }

    auto* profile = profileInfoScope.profileInfo.profile.get();

    profile->SetEndpoint(traceId, endpoint);

    // This method is called only once: when the trace closes
    profile->AddEndpointCount(endpoint, 1);
}

std::vector<UpscalingInfo> ProfileExporter::GetUpscalingInfos()
{
    std::vector<UpscalingInfo> samplingInfos;
    samplingInfos.reserve(_upscaledProviders.size());

    for (auto& provider : _upscaledProviders)
    {
        for (auto& upscalingInfo : provider->GetInfos())
        {
            samplingInfos.push_back(upscalingInfo);
        }
    }

    return samplingInfos;
}

std::vector<UpscalingPoissonInfo> ProfileExporter::GetUpscalingPoissonInfos()
{
    std::vector<UpscalingPoissonInfo> samplingInfos;
    samplingInfos.reserve(_upscaledPoissonProviders.size());

    for (auto& provider : _upscaledPoissonProviders)
    {
        samplingInfos.push_back(provider->GetPoissonInfo());
    }

    return samplingInfos;
}

void ProfileExporter::AddUpscalingRules(PprofBuilder* profile, std::vector<UpscalingInfo> const& upscalingInfos)
{
    for (auto const& upscalingInfo : upscalingInfos)
    {
        for (const auto& group : upscalingInfo.UpscaleGroups)
        {
            // upscaling could be based on count (exceptions) or value (lock contention)
            uint64_t sampled = group.SampledCount;
            if (group.SampledValue != 0)
            {
                sampled = group.SampledValue;
            }

            uint64_t real = group.RealCount;
            if (group.RealValue != 0)
            {
                real = group.RealValue;
            }
            auto succeeded = profile->AddUpscalingRuleProportional(upscalingInfo.Offsets, upscalingInfo.LabelName, group.Group, sampled, real);
            if (!succeeded)
            {
                Log::Warn(succeeded.message());
            }
        }
    }
}

void ProfileExporter::AddUpscalingPoissonRules(PprofBuilder* profile, std::vector<UpscalingPoissonInfo> const& upscalingInfos)
{
    for (auto const& upscalingInfo : upscalingInfos)
    {
        auto succeeded =
            profile->AddUpscalingRulePoisson(
                upscalingInfo.Offsets,
                std::string(),  // TODO: see how to get the type names / count
                std::string(),
                upscalingInfo.SumOffset,
                upscalingInfo.CountOffset,
                upscalingInfo.SamplingDistance
            );
        if (!succeeded)
        {
            Log::Warn(succeeded.message());
        }
    }
}

std::list<std::shared_ptr<Sample>> ProfileExporter::GetProcessSamples()
{
    std::list<std::shared_ptr<Sample>> samples;

    std::shared_ptr<Sample> sample(nullptr); // for process-level samples, we do not need to initialize
    for (auto const& provider : _processSamplesProviders)
    {
        auto processedSamples = provider->GetSamples();

        while (processedSamples->MoveNext(sample))
        {
            samples.push_back(sample);
        }
    }
    return samples;
}

void ProfileExporter::AddProcessSamples(PprofBuilder* profile, std::list<std::shared_ptr<Sample>> const& samples)
{
    for (auto const& sample : samples)
    {
        Add(profile, sample);
    }
}

bool ProfileExporter::Export(bool lastCall)
{
    bool exported = false;

    if (_allocationsRecorder != nullptr)
    {
        auto const& applicationInfo = _applicationStore->GetApplicationInfo("");
        auto filename = FileHelper::GenerateFilename("", AllocationsExtension, applicationInfo.ServiceName);
        auto filePath = fs::path(_outputPath) / filename;

        static bool firstFailure = true;
        if (!_allocationsRecorder->Serialize(filePath.string()))
        {
            if (firstFailure)
            {
                firstFailure = false;
                Log::Warn("Failed to serialize allocations in ", filePath);
            }
        }
    }

    std::vector<std::string_view> keys;

    {
        std::lock_guard lock(_perAppInfoLock);
        for (const auto& [key, _] : _perAppInfo)
        {
            keys.push_back(key);
        }
    }

    // The only reason found during tests was when no sample were collected but the tracer set endpoints.
    if (keys.empty())
    {
        Log::Debug("No sample has been collected. No profile will be sent.");
    }

    // upscaling rules apply for all the process.
    // In case of IIS, there may be multiple applications in the same process.
    // As the profiler samples the events for the process, the upscaling rules are the same
    // for all applications.
    auto upscalingInfos = GetUpscalingInfos();
    auto upscalingPoissonInfos = GetUpscalingPoissonInfos();

    // Process-level samples
    auto processSamples = GetProcessSamples();

    // additional content to be sent along the .pprof
    auto metricsFileContent = CreateMetricsFileContent();
    auto classHistogramContent = CreateClassHistogramContent();
    auto referenceTreeFiles = (_heapSnapshotManager != nullptr)
        ? _heapSnapshotManager->GetAndClearReferenceTreeContent()
        : std::vector<IHeapSnapshotManager::FileEntry>{};

    for (auto& runtimeId : keys)
    {
        std::unique_ptr<PprofBuilder> profile;
        int32_t samplesCount;
        int32_t exportsCount;

        // The goal here is to minimize the amount of time we hold the profileInfo lock.
        // The lock in ProfileInfoScope guarantees that nobody else is currently holding a reference to the profileInfo.
        // While inside the lock owned by the profileinfo scope, its profile is moved to the profile local variable
        // (i.e. the profileinfo will then contains a null profile field when the next sample will be added)
        // This way, we know that nobody else will ever use that profile again, and we can take our time to manipulate it
        // outside of the lock.
        {
            const auto scope = GetOrCreateInfo(runtimeId);

            // Get everything we need then release the lock
            profile = std::move(scope.profileInfo.profile);
            samplesCount = scope.profileInfo.samplesCount;

            // Count is incremented BEFORE creating and sending the .pprof
            // so that it will be possible to detect "missing" profiles
            // in the back end
            exportsCount = ++scope.profileInfo.exportsCount;

            scope.profileInfo.profile = nullptr;
            scope.profileInfo.samplesCount = 0;
        }

        const auto& applicationInfo = _applicationStore->GetApplicationInfo(std::string(runtimeId));

        if (profile == nullptr || samplesCount == 0)
        {
            Log::Debug("The profiler for application ", applicationInfo.ServiceName, " (runtime id:", runtimeId, ") have empty profile. Nothing will be sent.");

            continue;
        }

        if (_exporter == nullptr)
        {
            return false;
        }

        AddProcessSamples(profile.get(), processSamples);

        AddUpscalingRules(profile.get(), upscalingInfos);
        AddUpscalingPoissonRules(profile.get(), upscalingPoissonInfos);

        std::string runtimeIdString = std::string(runtimeId);
        tags additionalTags{{"env", applicationInfo.Environment},
                            {"version", applicationInfo.Version},
                            {"service", applicationInfo.ServiceName},
                            {"runtime-id", runtimeIdString},
                            {"profile_seq", std::to_string(exportsCount - 1)},
                            // Optim we can cache the number of cores in a string
                            {"number_of_cpu_cores", std::to_string(OsSpecificApi::GetProcessorCount())}};

        if (!applicationInfo.RepositoryUrl.empty())
        {
            additionalTags.emplace_back("git.repository_url", applicationInfo.RepositoryUrl);
        }
        if (!applicationInfo.CommitSha.empty())
        {
            additionalTags.emplace_back("git.commit.sha", applicationInfo.CommitSha);
        }

        auto filesToSend = std::vector<std::pair<std::string, std::vector<uint8_t>>>{};

        if (!metricsFileContent.empty())
        {
            Log::Debug("Attaching file: ", MetricsFilename, " (", metricsFileContent.size(), " bytes)");
            filesToSend.emplace_back(MetricsFilename,
                std::vector<uint8_t>(metricsFileContent.begin(), metricsFileContent.end()));
        }

        if (!classHistogramContent.empty())
        {
            Log::Debug("Attaching file: ", ClassHistogramFilename, " (", classHistogramContent.size(), " bytes)");
            filesToSend.emplace_back(ClassHistogramFilename,
                std::vector<uint8_t>(classHistogramContent.begin(), classHistogramContent.end()));
            additionalTags.emplace_back("profile_has_class_histogram", "true");
        }

        for (auto& [filename, content] : referenceTreeFiles)
        {
            Log::Debug("Attaching file: ", filename, " (", content.size(), " bytes)");
            filesToSend.emplace_back(filename, std::move(content));
            additionalTags.emplace_back("profile_has_reference_tree", "true");
        }

        std::string metadataJson = GetMetadataJson();
        std::string infoJson = GetInfoJson(runtimeIdString);
        std::string processTags = applicationInfo.ProcessTags;

        auto encodedProfile = profile->Serialize();
        auto error_code = _exporter->Send(encodedProfile, applicationInfo.ServiceName, std::move(additionalTags), std::move(filesToSend), std::move(metadataJson), std::move(infoJson), std::move(processTags));
        if (!error_code)
        {
            Log::Error(error_code.message());

            // TODO: send telemetry about failed sendings
        }

        exported &= error_code;
    }

    return exported;
}

std::string ProfileExporter::CreateMetricsFileContent() const
{
    // prepare metrics to be sent if any
    std::stringstream builder;
    auto metrics = _metricsRegistry.Collect();
    auto count = metrics.size();

    if (!metrics.empty())
    {
        builder << "[";
        for (auto const& metric : metrics)
        {
            builder << "["
                    << "\"" << metric.first << "\""
                    << ","
                    << metric.second
                    << "]";

            count--;
            if (count > 0)
            {
                builder << ", ";
            }
        }
        builder << "]";
    }
    return builder.str();
}

std::string ProfileExporter::CreateClassHistogramContent() const
{
    if (_heapSnapshotManager == nullptr)
    {
        return "";
    }

    // TODO: is it a problem to have the manager responsible for the serialization format?
    // Otherwhise, we would need to return the map while clearing it
    auto heapSnapshot = _heapSnapshotManager->GetAndClearHeapSnapshotText();
    if (!heapSnapshot.empty())
    {
        // prepare class histogram to be sent
        std::stringstream builder;
        builder << heapSnapshot;
        return builder.str();
    }

    return "";
}


std::string ProfileExporter::GetMetadataJson() const
{
    // in tests, the metadata provider might be null
    if (_metadataProvider == nullptr)
    {
        return "";
    }

    // TODO: check if we plan to update the metadata after the application starts
    //       otherwise, we could cache the result once for all.

    auto const& metadata = _metadataProvider->Get();
    if (metadata.empty())
    {
        return "";
    }
    auto sectionCount = metadata.size();
    auto currentSection = 0;

    // the json schema is supposed to send sections under the systemInfo element
    std::stringstream builder;
    builder << "{";
    ElementStart(builder, "systemInfo");
    for (auto const& [section, kvp] : metadata)
    {
        currentSection++;

        ElementStart(builder, section);
        auto keyCount = kvp.size();
        auto currentKey = 0;
        for (auto const& [key, value] : kvp)
        {
            currentKey++;
            AppendValue(builder, key, value);

            if (currentKey < keyCount)
            {
                builder << ", ";
            }
        }
        ElementEnd(builder);

        if (currentSection < sectionCount)
        {
            builder << ", ";
        }
    }
    ElementEnd(builder);
    builder << "}";

    return builder.str();
}

// Example of expected info json with SSI data and other runtime info:
//  "info": {
//     ...,
//     "profiler": {
//        "version": "3.2.0",
//        "ssi" : {
//           "mechanism": "injected_agent",
//         },
//        "activation": "injection",
//        "provider_list": "walltime_cpu_exceptions_allocations_lock_gc_heap_http_cpugc_threadslifetime",
//        "runtime" : {
//           "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
//           "os": "windows",
//           "version": "core-10.0",
//        }
//     },
//     "GC Config": {
//       list GC configuration (workstation/server/unknown today but maybe more via P/Invoke)
//     }
//     "System Properties": {
//        list overriden environment variables
//     }
//  }
//
std::string ProfileExporter::GetInfoJson(std::string& runtimeId) const
{
    // in tests, the metadata provider might be null
    if (_ssiManager == nullptr)
    {
        return "";
    }

    // TODO: check if we plan to update the metadata after the application starts
    //       otherwise, we could cache the result once for all.

    auto const& metadata = _metadataProvider->Get();
    if (metadata.empty())
    {
        return "";
    }

    // the json schema is supposed to send sections under the systemInfo element
    std::stringstream builder;
    builder << "{";
        AppendProfilerInfo(builder, runtimeId);
    builder << ",";
        AppendGcConfig(builder);

    // There might not be any env var so deal with the ',' in AppendEnvVars
    AppendEnvVars(builder, metadata);
    builder << "}";

    return builder.str();
}

void ProfileExporter::AppendProfilerInfo(std::stringstream& builder, std::string& runtimeId) const
{
    std::string activationValue;
    if (_configuration->GetEnablementStatus() == EnablementStatus::ManuallyEnabled)
    {
        activationValue = "manual";
    }
    else if (_configuration->GetEnablementStatus() == EnablementStatus::Auto)
    {
        activationValue = "auto";
    }
    else if (_configuration->GetEnablementStatus() == EnablementStatus::Standby)
    {
        activationValue = "standby"; // should never occur because the managed layer did not set the activation status
    }
    else
    {
        activationValue = "none";
    }

    ElementStart(builder, "profiler");
        AppendValue(builder, "version", PROFILER_VERSION);
        builder << ",";
        ElementStart(builder, "ssi");
            AppendValue(builder, "mechanism",
                (_ssiManager->GetDeploymentMode() == DeploymentMode::SingleStepInstrumentation) ? "injected_agent" : "none");
        ElementEnd(builder);
        builder << ",";
        AppendValue(builder, "activation", activationValue);
        builder << ",";
        AppendValue(builder, "provider_list", ProviderList);
        builder << ",";
        ElementStart(builder, "runtime");
            AppendValue(builder, "os", _runtimeInfo->GetOs());
            builder << ",";
            AppendValue(builder, "version", _runtimeInfo->GetClrString());
            builder << ",";
            AppendValue(builder, "runtime-id", runtimeId);
            builder << ",";
            AppendValue(builder, "start time", StartTime);
        ElementEnd(builder);
    ElementEnd(builder);
}

void ProfileExporter::AppendValueList(const tags& kvp, std::stringstream& builder) const
{
    auto keyCount = kvp.size();
    auto currentKey = 0;
    for (auto const& [key, value] : kvp)
    {
        currentKey++;
        AppendValue(builder, key, value);
        if (currentKey < keyCount)
        {
            builder << ", ";
        }
    }
}

void ProfileExporter::AppendEnvVars(std::stringstream& builder, const IMetadataProvider::metadata_t& metadata) const
{
    for (auto const& [section, kvp] : metadata)
    {
        if (section == MetadataProvider::SectionEnvVars)
        {
            builder << ", ";
            ElementStart(builder, "System Properties");
            AppendValueList(kvp, builder);
            ElementEnd(builder);
        }
        else if (section == MetadataProvider::SectionOverrides)
        {
            builder << ", ";
            ElementStart(builder, "System Overrides");
            AppendValueList(kvp, builder);
            ElementEnd(builder);
        }
    }
}

void ProfileExporter::AppendGcConfig(std::stringstream& builder) const
{
    // for .NET Framework, _gcSettingsProvider will be null
    std::string gcMode = "Unknown";

    if (_gcSettingsProvider != nullptr)
    {
        gcMode = (_gcSettingsProvider->GetMode() == GCMode::Server) ? "Server" : "Workstation";
    }

    // TODO: list GC settings  a "GC Config" node
    ElementStart(builder, "GC Config");
        AppendValue(builder, "GC Mode", gcMode);
    ElementEnd(builder);
}


fs::path ProfileExporter::CreatePprofOutputPath(IConfiguration* configuration)
{
    auto const& pprofOutputPath = configuration->GetProfilesOutputDirectory();
    if (pprofOutputPath.empty())
    {
        return pprofOutputPath;
    }

    // TODO: add process name to the path using Configuration::GetServiceName() and remove unsupported characters

    std::error_code errorCode;                              // not a problem if the directory already exists
    if (fs::create_directories(pprofOutputPath, errorCode) || (errorCode.value() == 0))
    {
        return pprofOutputPath;
    }

    Log::Error("Unable to create pprof output directory '", pprofOutputPath, "'. Error (code): ", errorCode.message(), " (", errorCode.value(), ")");

    return {};
}

ProfileExporter::ProfileInfo::ProfileInfo()
{
    profile = nullptr;
    samplesCount = 0;
    exportsCount = 0;
}

ProfileExporter::ProfileInfo::~ProfileInfo() = default;

ProfileExporter::ProfileInfoScope::ProfileInfoScope(ProfileExporter::ProfileInfo& profileInfo) :
    profileInfo(profileInfo),
    _lockGuard(profileInfo.lock)
{
}
