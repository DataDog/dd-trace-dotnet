// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfileExporter.h"

#include "Exception.h"
#include "Exporter.h"
#include "FfiHelper.h"
#include "IAllocationsRecorder.h"
#include "IApplicationStore.h"
#include "IEnabledProfilers.h"
#include "IMetadataProvider.h"
#include "IMetricsSender.h"
#include "IRuntimeInfo.h"
#include "ISamplesProvider.h"
#include "IUpscaleProvider.h"
#include "Log.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"
#include "Profile.h"
#include "Sample.h"
#include "ScopeFinalizer.h"
#include "dd_profiler_version.h"

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

std::string const ProfileExporter::ProfileExtension = ".pprof";
std::string const ProfileExporter::AllocationsExtension = ".balloc";

ProfileExporter::ProfileExporter(
    std::vector<SampleValueType> sampleTypeDefinitions,
    IConfiguration* configuration,
    IApplicationStore* applicationStore,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers,
    MetricsRegistry& metricsRegistry,
    IMetadataProvider* metadataProvider,
    IAllocationsRecorder* allocationsRecorder) :
    _sampleTypeDefinitions{std::move(sampleTypeDefinitions)},
    _applicationStore{applicationStore},
    _metricsRegistry{metricsRegistry},
    _metadataProvider{metadataProvider},
    _allocationsRecorder{allocationsRecorder}
{
    _exporter = CreateExporter(configuration, CreateTags(configuration, runtimeInfo, enabledProfilers));
    _pprofOutputPath = CreatePprofOutputPath(configuration);
    _metricsFileFolder = configuration->GetProfilesOutputDirectory();
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

std::unique_ptr<libdatadog::Exporter> ProfileExporter::CreateExporter(IConfiguration* configuration, libdatadog::Tags tags)
{
    try
    {
        auto exporterBuilder = libdatadog::Exporter::ExporterBuilder();

        auto& outputDirectory = configuration->GetProfilesOutputDirectory();
        if (!outputDirectory.empty())
        {
            exporterBuilder.WithFileExporter(outputDirectory);
        }

        exporterBuilder
            .SetLibraryName(LibraryName)
            .SetLibraryVersion(LibraryVersion)
            .SetLanguageFamily(LanguageFamily)
            .WithTags(std::move(tags));

        if (configuration->IsAgentless())
        {
            exporterBuilder.WithoutAgent(configuration->GetSite(), configuration->GetApiKey());
        }
        else
        {
            exporterBuilder.WithAgent(BuildAgentEndpoint(configuration));
        }

        return exporterBuilder.Build();
    }
    catch (libdatadog::Exception const& e)
    {
        Log::Error("Failed to create the exporter: ", e.what());
        return nullptr;
    }
}

std::unique_ptr<libdatadog::Profile> ProfileExporter::CreateProfile(std::string serviceName)
{
    return std::make_unique<libdatadog::Profile>(_sampleTypeDefinitions, ProfilePeriodType, ProfilePeriodUnit, std::move(serviceName));
}

void ProfileExporter::RegisterUpscaleProvider(IUpscaleProvider* provider)
{
    assert(provider != nullptr);
    _upscaledProviders.push_back(provider);
}

void ProfileExporter::RegisterProcessSamplesProvider(ISamplesProvider* provider)
{
    assert(provider != nullptr);
    _processSamplesProviders.push_back(provider);
}

libdatadog::Tags ProfileExporter::CreateTags(
    IConfiguration* configuration,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers)
{
    auto tags = libdatadog::Tags();

    for (auto const& [name, value] : CommonTags)
    {
        tags.Add(name, value);
    }

    tags.Add("process_id", ProcessId);
    tags.Add("host", configuration->GetHostname());
    tags.Add("runtime_version", runtimeInfo->GetClrString());

    // list of enabled profilers
    std::string profilersTag = GetEnabledProfilersTag(enabledProfilers);
    tags.Add("profiler_list", profilersTag);

    // runtime_platform (os and version later)
    tags.Add("runtime_os", runtimeInfo->GetOs());

    for (auto const& [name, value] : configuration->GetUserTags())
    {
        tags.Add(name, value);
    }

    return tags;
}

std::string ProfileExporter::GetEnabledProfilersTag(IEnabledProfilers* enabledProfilers)
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

    return buffer.str();
}

std::string ProfileExporter::BuildAgentEndpoint(IConfiguration* configuration)
{
    // handle "with agent" case
    auto url = configuration->GetAgentUrl(); // copy expected here

    if (url.empty())
    {
        // Agent mode

#if _WINDOWS
        const std::string& namePipeName = configuration->GetNamedPipeName();
        if (!namePipeName.empty())
        {
            url = R"(windows:\\.\pipe\)" + namePipeName;
        }
#else
        std::error_code ec; // fs::exists might throw if no error_code parameter is provided
        const std::string socketPath = "/var/run/datadog/apm.socket";
        if (fs::exists(socketPath, ec))
        {
            url = "unix://" + socketPath;
        }

#endif

        if (url.empty())
        {
            // Use default HTTP endpoint
            std::stringstream oss;
            oss << "http://" << configuration->GetAgentHost() << ":" << configuration->GetAgentPort();
            url = oss.str();
        }
    }

    Log::Info("Using agent endpoint ", url);

    return url;
}

ProfileExporter::ProfileInfoScope ProfileExporter::GetOrCreateInfo(std::string_view runtimeId)
{
    std::lock_guard lock(_perAppInfoLock);

    auto& profileInfo = _perAppInfo[runtimeId];

    return profileInfo;
}

void ProfileExporter::Add(libdatadog::Profile* profile, std::shared_ptr<Sample> const& sample)
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
        samplingInfos.push_back(provider->GetInfo());
    }

    return samplingInfos;
}

void ProfileExporter::AddUpscalingRules(libdatadog::Profile* profile, std::vector<UpscalingInfo> const& upscalingInfos)
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

std::list<std::shared_ptr<Sample>> ProfileExporter::GetProcessSamples()
{
    std::list<std::shared_ptr<Sample>> samples;
    for (auto const& provider : _processSamplesProviders)
    {
        samples.splice(samples.end(), provider->GetSamples());
    }
    return samples;
}

void ProfileExporter::AddProcessSamples(libdatadog::Profile* profile, std::list<std::shared_ptr<Sample>> const& samples)
{
    for (auto const& sample : samples)
    {
        Add(profile, sample);
    }
}

bool ProfileExporter::Export()
{
    bool exported = false;

    int32_t idx = 0;

    if (_allocationsRecorder != nullptr)
    {
        const auto& applicationInfo = _applicationStore->GetApplicationInfo(std::string(""));
        auto filePath = GenerateFilePath(applicationInfo.ServiceName, idx, AllocationsExtension);
        static bool firstFailure = true;
        if (!_allocationsRecorder->Serialize(filePath))
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

    // Process-level samples
    auto processSamples = GetProcessSamples();

    for (auto& runtimeId : keys)
    {
        std::unique_ptr<libdatadog::Profile> profile;
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

        auto additionalTags = libdatadog::Tags{{"env", applicationInfo.Environment},
                                               {"version", applicationInfo.Version},
                                               {"service", applicationInfo.ServiceName},
                                               {"runtime-id", std::string(runtimeId)},
                                               {"profile_seq", std::to_string(exportsCount - 1)},
                                               // Optim we can cache the number of cores in a string
                                               {"number_of_cpu_cores", std::to_string(OsSpecificApi::GetProcessorCount())}};

        if (!applicationInfo.RepositoryUrl.empty())
        {
            additionalTags.Add("git.repository_url", applicationInfo.RepositoryUrl);
        }
        if (!applicationInfo.CommitSha.empty())
        {
            additionalTags.Add("git.commit.sha", applicationInfo.CommitSha);
        }

        auto filesToSend = std::vector<std::pair<std::string, std::string>>{{MetricsFilename, CreateMetricsFileContent()}};
        std::string json = GetMetadata();

        auto error_code = _exporter->Send(profile.get(), std::move(additionalTags), std::move(filesToSend), std::move(json));
        if (!error_code)
        {
            Log::Error(error_code.message());
        }
        exported &= error_code;
    }

    return exported;
}

void ProfileExporter::SaveJsonToDisk(const std::string prefix, const std::string& content) const
{
    std::stringstream filename;
    filename << prefix << "-" << std::to_string(OpSysTools::GetProcId()) << ".json";
    auto filepath = fs::path(_metricsFileFolder) / filename.str();
    std::ofstream file{filepath.string(), std::ios::out | std::ios::binary};

    file.write(content.c_str(), content.size());
    file.close();
}

std::string ProfileExporter::GenerateFilePath(const std::string& applicationName, int32_t idx, const std::string& extension) const
{
    auto time = std::time(nullptr);
    struct tm buf = {};

#ifdef _WINDOWS
    localtime_s(&buf, &time);
#else
    localtime_r(&time, &buf);
#endif

    std::stringstream oss;
    oss << applicationName + "_" << ProcessId << "_" << std::put_time(&buf, "%F_%H-%M-%S") << "_" << idx
        << extension;
    auto pprofFilename = oss.str();

    auto pprofFilePath = fs::path(_pprofOutputPath) / pprofFilename;

    return pprofFilePath.string();
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

std::string ProfileExporter::GetMetadata() const
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
    builder << "{ \"systemInfo\": ";
    builder << "{";
    for (auto const& [section, kvp] : metadata)
    {
        currentSection++;

        builder << "\"";
        builder << section;
        builder << "\":";
        builder << "{";

        auto keyCount = kvp.size();
        auto currentKey = 0;
        for (auto const& [key, value] : kvp)
        {
            currentKey++;
            builder << "\"";
            builder << key;
            builder << "\":";
            builder << "\"";
            builder << value;
            builder << "\"";

            if (currentKey < keyCount)
            {
                builder << ", ";
            }
        }
        builder << "}";

        if (currentSection < sectionCount)
        {
            builder << ", ";
        }
    }
    builder << "}}";

    return builder.str();
}

fs::path ProfileExporter::CreatePprofOutputPath(IConfiguration* configuration)
{
    auto const& pprofOutputPath = configuration->GetProfilesOutputDirectory();
    if (pprofOutputPath.empty())
    {
        return pprofOutputPath;
    }

    // TODO: add process name to the path using Configuration::GetServiceName() and remove unsupported characters

    std::error_code errorCode;
    if (fs::create_directories(pprofOutputPath, errorCode) || errorCode)
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
