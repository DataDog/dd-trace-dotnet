// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibddprofExporter.h"

#include "FfiHelper.h"
#include "ScopeFinalizer.h"
#include "IApplicationStore.h"
#include "IMetricsSender.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "OpSysTools.h"
#include "Sample.h"
#include "dd_profiler_version.h"
#include "IRuntimeInfo.h"
#include "IEnabledProfilers.h"
#include "IAllocationsRecorder.h"

#include <cassert>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <string.h>
#include <time.h>

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

#define BUFFER_MAX_SIZE 512

tags LibddprofExporter::CommonTags = {
    {"language", "dotnet"},
    {"profiler_version", PROFILER_VERSION},
#ifdef BIT64
    {"process_architecture", "x64"},
#else
    {"process_architecture", "x86"}
#endif
};

// need to be static so it leave longer for the shared library
std::string const LibddprofExporter::ProcessId = std::to_string(OpSysTools::GetProcId());

int32_t const LibddprofExporter::RequestTimeOutMs = 10000;

std::string const LibddprofExporter::LibraryName = "dd-profiling-dotnet";

std::string const LibddprofExporter::LibraryVersion = PROFILER_VERSION;

std::string const LibddprofExporter::LanguageFamily = "dotnet";

std::string const LibddprofExporter::RequestFileName = "auto.pprof";

std::string const LibddprofExporter::ProfilePeriodType = "RealTime";

std::string const LibddprofExporter::ProfilePeriodUnit = "Nanoseconds";

std::string const LibddprofExporter::MetricsFilename = "metrics.json";

std::string const LibddprofExporter::ProfileExtension = ".pprof";
std::string const LibddprofExporter::AllocationsExtension = ".balloc";

LibddprofExporter::LibddprofExporter(
    std::vector<SampleValueType>&& sampleTypeDefinitions,
    IConfiguration* configuration,
    IApplicationStore* applicationStore,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers,
    MetricsRegistry& metricsRegistry,
    IAllocationsRecorder* allocationsRecorder)
    :
    _sampleTypeDefinitions{std::move(sampleTypeDefinitions)},
    _locationsAndLinesSize{512},
    _applicationStore{applicationStore},
    _metricsRegistry{metricsRegistry},
    _allocationsRecorder{allocationsRecorder}
{
    _exporterBaseTags = CreateTags(configuration, runtimeInfo, enabledProfilers);
    _endpoint = CreateEndpoint(configuration);
    _pprofOutputPath = CreatePprofOutputPath(configuration);
    _locations.resize(_locationsAndLinesSize);
    _lines.resize(_locationsAndLinesSize);
    _metricsFileFolder = configuration->GetProfilesOutputDirectory();
}

LibddprofExporter::~LibddprofExporter()
{
    std::lock_guard lock(_perAppInfoLock);

    for (auto& [runtimeId, appInfo] : _perAppInfo)
    {
        {
            std::lock_guard lockProfile(appInfo.lock);

            if (appInfo.profile != nullptr)
            {
                ddog_prof_Profile_drop(appInfo.profile);
            }

            appInfo.profile = nullptr;
        }
    }
    _perAppInfo.clear();
}

ddog_prof_Exporter* LibddprofExporter::CreateExporter(const ddog_Vec_Tag* tags, ddog_Endpoint endpoint)
{
    auto result = ddog_prof_Exporter_new(
        FfiHelper::StringToCharSlice(LibraryName),
        FfiHelper::StringToCharSlice(LibraryVersion),
        FfiHelper::StringToCharSlice(LanguageFamily),
        tags,
        endpoint);

    if (result.tag == DDOG_PROF_EXPORTER_NEW_RESULT_OK)
    {
        return result.ok;
    }
    else
    {
        auto errorMessage = ddog_Error_message(&result.err);
        Log::Error("Failed to create the exporter: ", std::string_view(errorMessage.ptr, errorMessage.len));
        ddog_Error_drop(&result.err);
        return nullptr;
    }
}

struct ddog_prof_Profile* LibddprofExporter::CreateProfile()
{
    std::vector<ddog_prof_ValueType> samplesTypes;
    samplesTypes.reserve(_sampleTypeDefinitions.size());

    for (auto const& type : _sampleTypeDefinitions)
    {
        samplesTypes.push_back(FfiHelper::CreateValueType(type.Name, type.Unit));
    }

    struct ddog_prof_Slice_ValueType sample_types = {samplesTypes.data(), samplesTypes.size()};

    auto period_value_type = FfiHelper::CreateValueType(ProfilePeriodType, ProfilePeriodUnit);

    auto period = ddog_prof_Period{};
    period.type_ = period_value_type;
    period.value = 1;

    return ddog_prof_Profile_new(sample_types, &period, nullptr);
}

LibddprofExporter::Tags LibddprofExporter::CreateTags(
    IConfiguration* configuration,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers)
{
    auto tags = LibddprofExporter::Tags{};

    for (auto const& [name, value] : CommonTags)
    {
        tags.Add(name, value);
    }

    tags.Add("process_id", ProcessId);
    tags.Add("host", configuration->GetHostname());

    // runtime_version:
    //    framework-4.8
    //    core-6.0
    std::stringstream buffer;
    if (runtimeInfo->IsDotnetFramework())
    {
        buffer << "framework";
    }
    else
    {
        buffer << "core";
    }
    buffer << "-" << std::dec << runtimeInfo->GetDotnetMajorVersion() << "." << runtimeInfo->GetDotnetMinorVersion();
    tags.Add("runtime_version", buffer.str());

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

std::string LibddprofExporter::GetEnabledProfilersTag(IEnabledProfilers* enabledProfilers)
{
    const char* separator = "_";  // ',' are not allowed and +/SPACE would be transformed into '_' anyway
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

ddog_Endpoint LibddprofExporter::CreateEndpoint(IConfiguration* configuration)
{
    if (configuration->IsAgentless())
    {
        // handle "agentless" case
        auto const& site = configuration->GetSite();
        auto const& apiKey = configuration->GetApiKey();

        return ddog_Endpoint_agentless(FfiHelper::StringToCharSlice(site), FfiHelper::StringToCharSlice(apiKey));
    }

    // handle "with agent" case
    auto const& url = configuration->GetAgentUrl();
    if (!url.empty())
    {
        _agentUrl = url;
    }
    else
    {
        // Agent mode

        std::string agentUrl;
#if _WINDOWS
        const std::string& namePipeName = configuration->GetNamedPipeName();
        if (!namePipeName.empty())
        {
            agentUrl = R"(windows:\\.\pipe\)" + namePipeName;
        }
#else
        std::error_code ec; // fs::exists might throw if no error_code parameter is provided
        const std::string socketPath = "/var/run/datadog/apm.socket";
        if (fs::exists(socketPath, ec))
        {
            agentUrl = "unix://" + socketPath;
        }

#endif

        if (!agentUrl.empty())
        {
            _agentUrl = agentUrl;
        }
        else
        {
            // Use default HTTP endpoint
            std::stringstream oss;
            oss << "http://" << configuration->GetAgentHost() << ":" << configuration->GetAgentPort();
            _agentUrl = oss.str();
        }
    }

    Log::Info("Using agent endpoint ", _agentUrl);

    return ddog_Endpoint_agent(FfiHelper::StringToCharSlice(_agentUrl));
}

LibddprofExporter::ProfileInfoScope LibddprofExporter::GetInfo(std::string_view runtimeId)
{
    std::lock_guard lock(_perAppInfoLock);

    auto& profileInfo = _perAppInfo[runtimeId];

    return profileInfo;
}

void LibddprofExporter::Add(std::shared_ptr<Sample> const& sample)
{
    auto profileInfoScope = GetInfo(sample->GetRuntimeId());

    if (profileInfoScope.profileInfo.profile == nullptr)
    {
        profileInfoScope.profileInfo.profile = CreateProfile();
    }

    auto* profile = profileInfoScope.profileInfo.profile;

    auto const& callstack = sample->GetCallstack();
    auto nbFrames = callstack.size();

    if (nbFrames > _locationsAndLinesSize)
    {
        _locationsAndLinesSize = nbFrames;
        _locations.resize(_locationsAndLinesSize);
        _lines.resize(_locationsAndLinesSize);
    }

    std::size_t idx = 0UL;
    for (auto const& frame : callstack)
    {
        auto& line = _lines[idx];
        auto& location = _locations[idx];

        line = {};
        line.function.filename = {};
        line.function.start_line = 0;
        line.function.name = FfiHelper::StringToCharSlice(frame.second);

        // add filename mapping
        location.mapping = {};
        location.mapping.filename = FfiHelper::StringToCharSlice(frame.first);
        location.address = 0; // TODO check if we can get that information in the provider
        location.lines = {&line, 1};
        location.is_folded = false;

        ++idx;
    }

    auto ffiSample = ddog_prof_Sample{};
    ffiSample.locations = {_locations.data(), nbFrames};

    // Labels
    auto const& labels = sample->GetLabels();
    auto const& numericLabels = sample->GetNumericLabels();
    std::vector<ddog_prof_Label> ffiLabels;
    ffiLabels.reserve(labels.size() + numericLabels.size());

    for (auto const& [label, value] : labels)
    {
        ffiLabels.push_back({{label.data(), label.size()}, {value.data(), value.size()}});
    }

    for (auto const& [label, value] : numericLabels)
    {
        ffiLabels.push_back({{label.data(), label.size()}, {nullptr, 0}, value});
    }

    ffiSample.labels = {ffiLabels.data(), ffiLabels.size()};

    // values
    auto const& values = sample->GetValues();
    ffiSample.values = {values.data(), values.size()};

    // TODO: add timestamps when available

    auto result = ddog_prof_Profile_add(profile, ffiSample);
    profileInfoScope.profileInfo.samplesCount++;
}

void LibddprofExporter::SetEndpoint(const std::string& runtimeId, uint64_t traceId, const std::string& endpoint)
{
    const auto profileInfoScope = GetInfo(runtimeId);

    if (profileInfoScope.profileInfo.profile == nullptr)
    {
        profileInfoScope.profileInfo.profile = CreateProfile();
    }

    auto* profile = profileInfoScope.profileInfo.profile;

    auto endpointName = FfiHelper::StringToCharSlice(endpoint);

    ddog_prof_Profile_set_endpoint(profile, traceId, endpointName);

    // This method is called only once: when the trace closes
    ddog_prof_Profile_add_endpoint_count(profile, endpointName, 1);
}

bool LibddprofExporter::Export()
{
    bool exported = false;

    int32_t idx = 0;

    if (_allocationsRecorder != nullptr)
    {
        const auto& applicationInfo = _applicationStore->GetApplicationInfo(std::string(""));
        auto filePath = GenerateFilePath(applicationInfo.ServiceName, idx, AllocationsExtension);

        if (!_allocationsRecorder->Serialize(filePath))
        {
            Log::Warn("Failed to serialize allocations in ", filePath);
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

    for (auto& runtimeId : keys)
    {
        ddog_prof_Profile* profile;
        int32_t samplesCount;
        int32_t exportsCount;

        // The goal here is to minimize the amount of time we hold the profileInfo lock.
        // The lock in ProfileInfoScope guarantees that nobody else is currently holding a reference to the profileInfo.
        // While inside the lock owned by the profileinfo scope, its profile is moved to the profile local variable
        // (i.e. the profileinfo will then contains a null profile field when the next sample will be added)
        // This way, we know that nobody else will ever use that profile again, and we can take our time to manipulate it
        // outside of the lock.
        {
            const auto scope = GetInfo(runtimeId);

            // Get everything we need then release the lock
            profile = scope.profileInfo.profile;
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

        auto profileAutoDelete = ProfileAutoDelete{profile};
        auto serializedProfile = SerializedProfile{profile};
        if (!serializedProfile.IsValid())
        {
            Log::Error("Unable to serialize the profile. No profile will be sent.");
            return false;
        }

        if (!_pprofOutputPath.empty())
        {
            ExportToDisk(applicationInfo.ServiceName, serializedProfile, idx++);
        }

        auto* exporter = CreateExporter(_exporterBaseTags.GetFfiTags(), _endpoint);

        if (exporter == nullptr)
        {
            Log::Error("Unable to create exporter for application ", runtimeId);
            return false;
        }

        Tags additionalTags;
        additionalTags.Add("env", applicationInfo.Environment);
        additionalTags.Add("version", applicationInfo.Version);
        additionalTags.Add("service", applicationInfo.ServiceName);
        additionalTags.Add("runtime-id", std::string(runtimeId));
        additionalTags.Add("profile_seq", std::to_string(exportsCount - 1));
        additionalTags.Add("number_of_cpu_cores", std::to_string(OsSpecificApi::GetProcessorCount()));

        auto* request = CreateRequest(serializedProfile, exporter, additionalTags);
        if (request != nullptr)
        {
            exported &= Send(request, exporter);
        }
        else
        {
            exported = false;
            Log::Error("Unable to create a request to send the profile.");
        }
        ddog_prof_Exporter_drop(exporter);
    }

    return exported;
}

void LibddprofExporter::SaveMetricsToDisk(const std::string& content) const
{
    std::stringstream filename;
    filename << "metrics-" << std::to_string(OpSysTools::GetProcId()) << ".json";
    auto filepath = fs::path(_metricsFileFolder) / filename.str();
    std::ofstream file{filepath.string(), std::ios::out | std::ios::binary};

    file.write(content.c_str(), content.size());
    file.close();
}

std::string LibddprofExporter::GenerateFilePath(const std::string& applicationName, int32_t idx, const std::string& extension) const
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

void LibddprofExporter::ExportToDisk(const std::string& applicationName, SerializedProfile const& encodedProfile, int32_t idx)
{
    auto pprofFilePath = GenerateFilePath(applicationName, idx, ProfileExtension);

    std::ofstream file{pprofFilePath, std::ios::out | std::ios::binary};

    auto buffer = encodedProfile.GetBuffer();

    file.write((char const*)buffer.ptr, buffer.len);
    file.close();

    if (file.fail())
    {
        char message[BUFFER_MAX_SIZE];
        auto errorCode = errno;
#ifdef _WINDOWS
        strerror_s(message, BUFFER_MAX_SIZE, errorCode);
#else
        strerror_r(errorCode, message, BUFFER_MAX_SIZE);
#endif
        Log::Error("Unable to write profiles on disk: ", pprofFilePath, ". Message (code): ", message, " (", errorCode, ")");
    }
    else
    {
        Log::Debug("Profile serialized in ", pprofFilePath);
    }
}

std::string LibddprofExporter::CreateMetricsFileContent() const
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

ddog_prof_Exporter_Request* LibddprofExporter::CreateRequest(SerializedProfile const& encodedProfile, ddog_prof_Exporter* exporter, const Tags& additionalTags) const
{
    // endpoints
    auto* endpointsStats = encodedProfile.GetEndpointsStats();

    // profile
    auto start = encodedProfile.GetStart();
    auto end = encodedProfile.GetEnd();
    auto profileBuffer = encodedProfile.GetBuffer();
    ddog_prof_Exporter_File profile{FfiHelper::StringToCharSlice(RequestFileName), ddog_Vec_U8_as_slice(&profileBuffer)};

    ddog_prof_Exporter_File filesArray[2]{};
    // profile
    filesArray[0] = profile;

    struct ddog_prof_Exporter_Slice_File files{};
    files.len = 1;
    files.ptr = filesArray;

    auto metricsFileContent = CreateMetricsFileContent();

    if (!metricsFileContent.empty())
    {
        // Add metric files
#ifdef _DEBUG
        SaveMetricsToDisk(metricsFileContent);
#endif
        ddog_Slice_U8 metricsFileSlice{reinterpret_cast<const uint8_t*>(metricsFileContent.c_str()), metricsFileContent.size()};

        filesArray[1] = {FfiHelper::StringToCharSlice(MetricsFilename), metricsFileSlice};
        files.len = 2;
    }

    auto result = ddog_prof_Exporter_Request_build(exporter, start, end, files, additionalTags.GetFfiTags(), endpointsStats, RequestTimeOutMs);
    if (result.tag == DDOG_PROF_EXPORTER_REQUEST_BUILD_RESULT_ERR)
    {
        auto errorMessage = ddog_Error_message(&result.err);
        Log::Error("Failed to build request: ", std::string_view(errorMessage.ptr, errorMessage.len));
        ddog_Error_drop(&result.err);
        return nullptr;
    }

    return result.ok;
}

bool LibddprofExporter::Send(ddog_prof_Exporter_Request* request, ddog_prof_Exporter* exporter)
{
    assert(request != nullptr);

    auto result = ddog_prof_Exporter_send(exporter, &request, nullptr);

    on_leave
    {
        if (result.tag == DDOG_PROF_EXPORTER_SEND_RESULT_ERR)
            ddog_Error_drop(&result.err);
    };

    if (result.tag == DDOG_PROF_EXPORTER_SEND_RESULT_ERR)
    {
        auto errorMessage = ddog_Error_message(&result.err);
        Log::Error("Failed to send profile (", std::string_view(errorMessage.ptr, errorMessage.len), ")"); // NOLINT
        return false;
    }

    // Although we expect only 200, this range represents successful sends
    auto failed = result.http_response.code < 200 || result.http_response.code >= 300;
    if (failed)
    {
        Log::Error("Failed to send profile. Http code: ", result.http_response.code);
    }

    return !failed;
}

fs::path LibddprofExporter::CreatePprofOutputPath(IConfiguration* configuration)
{
    auto const& pprofOutputPath = configuration->GetProfilesOutputDirectory();
    if (pprofOutputPath.empty())
    {
        return pprofOutputPath;
    }

    // TODO: add process name to the path using Configuration::GetServiceName() and remove unsupported characters

    std::error_code errorCode;
    if (fs::create_directories(pprofOutputPath, errorCode) || (errorCode.value() == 0))
    {
        return pprofOutputPath;
    }

    Log::Error("Unable to create pprof output directory '", pprofOutputPath, "'. Error (code): ", errorCode.message(), " (", errorCode.value(), ")");

    return {};
}

//
// LibddprofExporter::SerializedProfile class
//
LibddprofExporter::SerializedProfile::SerializedProfile(ddog_prof_Profile* profile) :
    _encodedProfile{ddog_prof_Profile_serialize(profile, nullptr, nullptr)}
{
}

bool LibddprofExporter::SerializedProfile::IsValid() const
{
    return _encodedProfile.tag == DDOG_PROF_PROFILE_SERIALIZE_RESULT_OK;
}

LibddprofExporter::SerializedProfile::~SerializedProfile()
{
    if (!IsValid())
    {
        ddog_Error_drop(&_encodedProfile.err);
    }
}

ddog_Vec_U8 LibddprofExporter::SerializedProfile::GetBuffer() const
{
    return _encodedProfile.ok.buffer;
}

ddog_Timespec LibddprofExporter::SerializedProfile::GetStart() const
{
    return _encodedProfile.ok.start;
}

ddog_Timespec LibddprofExporter::SerializedProfile::GetEnd() const
{
    return _encodedProfile.ok.end;
}

ddog_prof_ProfiledEndpointsStats* LibddprofExporter::SerializedProfile::GetEndpointsStats() const
{
    return _encodedProfile.ok.endpoints_stats;
}

//
// LibddprofExporter::Tags class
//

LibddprofExporter::Tags::Tags() :
    _ffiTags{ddog_Vec_Tag_new()}
{
}

LibddprofExporter::Tags::~Tags() noexcept
{
    ddog_Vec_Tag_drop(_ffiTags);
}

LibddprofExporter::Tags::Tags(Tags&& other) noexcept
{
    *this = std::move(other);
}

LibddprofExporter::Tags& LibddprofExporter::Tags::operator=(LibddprofExporter::Tags&& other) noexcept
{
    if (this == &other)
    {
        return *this;
    }

    _ffiTags.ptr = std::exchange(other._ffiTags.ptr, nullptr);
    _ffiTags.capacity = std::exchange(other._ffiTags.capacity, 0);
    _ffiTags.len = std::exchange(other._ffiTags.len, 0);
    return *this;
}

void LibddprofExporter::Tags::Add(std::string const& labelName, std::string const& labelValue)
{
    auto ffiName = FfiHelper::StringToCharSlice(labelName);
    auto ffiValue = FfiHelper::StringToCharSlice(labelValue);

    auto pushResult = ddog_Vec_Tag_push(&_ffiTags, ffiName, ffiValue);
    if (pushResult.tag == DDOG_VEC_TAG_PUSH_RESULT_ERR)
    {
        auto errorMessage = ddog_Error_message(&pushResult.err);
        Log::Debug("Failed to add tag: ", std::string_view(errorMessage.ptr, errorMessage.len));
        ddog_Error_drop(&pushResult.err);
    }
}

const ddog_Vec_Tag* LibddprofExporter::Tags::GetFfiTags() const
{
    return &_ffiTags;
}

//
// LibddprofExporter::ProfileAutoDelete class
//

LibddprofExporter::ProfileAutoDelete::ProfileAutoDelete(struct ddog_prof_Profile* profile) :
    _profile{profile}
{
}

LibddprofExporter::ProfileAutoDelete::~ProfileAutoDelete()
{
    ddog_prof_Profile_drop(_profile);
}

//
// LibddprofExporter::ProfileInfo class
//

LibddprofExporter::ProfileInfo::ProfileInfo()
{
    profile = nullptr;
    samplesCount = 0;
    exportsCount = 0;
}

LibddprofExporter::ProfileInfoScope::ProfileInfoScope(LibddprofExporter::ProfileInfo& profileInfo) :
    profileInfo(profileInfo),
    _lockGuard(profileInfo.lock)
{
}
