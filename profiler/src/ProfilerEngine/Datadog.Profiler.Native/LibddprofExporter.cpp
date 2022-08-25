// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibddprofExporter.h"

#include "FfiHelper.h"
#include "ScopeFinalizer.h"
#include "IApplicationStore.h"
#include "IMetricsSender.h"
#include "Log.h"
#include "OpSysTools.h"
#include "Sample.h"
#include "dd_profiler_version.h"
#include "IRuntimeInfo.h"
#include "IEnabledProfilers.h"

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

std::string const LibddprofExporter::LanguageFamily = "dotnet";

std::string const LibddprofExporter::RequestFileName = "auto.pprof";

std::string const LibddprofExporter::ProfilePeriodType = "RealTime";

std::string const LibddprofExporter::ProfilePeriodUnit = "Nanoseconds";

LibddprofExporter::LibddprofExporter(
    IConfiguration* configuration,
    IApplicationStore* applicationStore,
    IRuntimeInfo* runtimeInfo,
    IEnabledProfilers* enabledProfilers)
    :
    _locationsAndLinesSize{512},
    _applicationStore{applicationStore}
{
    _exporterBaseTags = CreateTags(configuration, runtimeInfo, enabledProfilers);
    _endpoint = CreateEndpoint(configuration);
    _pprofOutputPath = CreatePprofOutputPath(configuration);
    _locations.resize(_locationsAndLinesSize);
    _lines.resize(_locationsAndLinesSize);
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
                ddprof_ffi_Profile_free(appInfo.profile);
            }

            appInfo.profile = nullptr;
        }
    }
    _perAppInfo.clear();
}

ddprof_ffi_ProfileExporterV3* LibddprofExporter::CreateExporter(const ddprof_ffi_Vec_tag* tags, ddprof_ffi_EndpointV3 endpoint)
{
    auto result = ddprof_ffi_ProfileExporterV3_new(FfiHelper::StringToCharSlice(LanguageFamily), tags, endpoint);
    if (result.tag == DDPROF_FFI_NEW_PROFILE_EXPORTER_V3_RESULT_OK)
    {
        return result.ok;
    }
    else
    {
        Log::Error("Failed to create the exporter: ", result.err.ptr);
        return nullptr;
    }
}

struct ddprof_ffi_Profile* LibddprofExporter::CreateProfile()
{
    std::vector<ddprof_ffi_ValueType> samplesTypes;
    samplesTypes.reserve(sizeof(SampleTypeDefinitions) / sizeof(SampleTypeDefinitions[0]));

    for (auto const& type : SampleTypeDefinitions)
    {
        samplesTypes.push_back(FfiHelper::CreateValueType(type.Name, type.Unit));
    }

    struct ddprof_ffi_Slice_value_type sample_types = {samplesTypes.data(), samplesTypes.size()};

    auto period_value_type = FfiHelper::CreateValueType(ProfilePeriodType, ProfilePeriodUnit);

    auto period = ddprof_ffi_Period{};
    period.type_ = period_value_type;
    period.value = 1;

    return ddprof_ffi_Profile_new(sample_types, &period, nullptr);
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

    return buffer.str();
}


ddprof_ffi_EndpointV3 LibddprofExporter::CreateEndpoint(IConfiguration* configuration)
{
    if (configuration->IsAgentless())
    {
        // handle "agentless" case
        auto const& site = configuration->GetSite();
        auto const& apiKey = configuration->GetApiKey();

        return ddprof_ffi_EndpointV3_agentless(FfiHelper::StringToCharSlice(site), FfiHelper::StringToCharSlice(apiKey));
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

#if _WINDOWS
        bool useDefaultDomainSocket = false;
#else
        std::error_code ec; // fs::exists might throw if no error_code parameter is provided
        bool useDefaultDomainSocket = fs::exists("/var/run/datadog/apm.socket", ec);
#endif

        if (useDefaultDomainSocket)
        {
            _agentUrl = "unix:///var/run/datadog/apm.socket";
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

    return ddprof_ffi_EndpointV3_agent(FfiHelper::StringToCharSlice(_agentUrl));
}

LibddprofExporter::ProfileInfoScope LibddprofExporter::GetInfo(std::string_view runtimeId)
{
    std::lock_guard lock(_perAppInfoLock);

    auto& profileInfo = _perAppInfo[runtimeId];

    return profileInfo;
}

void LibddprofExporter::Add(Sample const& sample)
{
    auto profileInfoScope = GetInfo(sample.GetRuntimeId());

    if (profileInfoScope.profileInfo.profile == nullptr)
    {
        profileInfoScope.profileInfo.profile = CreateProfile();
    }

    auto* profile = profileInfoScope.profileInfo.profile;

    auto const& callstack = sample.GetCallstack();
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

    auto ffiSample = ddprof_ffi_Sample{};
    ffiSample.locations = {_locations.data(), nbFrames};

    // Labels
    auto const& labels = sample.GetLabels();
    std::vector<ddprof_ffi_Label> ffiLabels;
    ffiLabels.reserve(labels.size());

    for (auto const& [label, value] : labels)
    {
        ffiLabels.push_back({{label.data(), label.size()}, {value.data(), value.size()}});
    }
    ffiSample.labels = {ffiLabels.data(), ffiLabels.size()};

    // values
    auto const& values = sample.GetValues();
    ffiSample.values = {values.data(), values.size()};

    ddprof_ffi_Profile_add(profile, ffiSample);
    profileInfoScope.profileInfo.samplesCount++;
}

bool LibddprofExporter::Export()
{
    bool exported = false;

    int32_t idx = 0;

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
        ddprof_ffi_Profile* profile;
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
        ddprof_ffi_ProfileExporterV3_delete(exporter);
    }
    return exported;
}

std::string LibddprofExporter::GeneratePprofFilePath(const std::string& applicationName, int32_t idx) const
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
        << ".pprof";
    auto pprofFilename = oss.str();

    auto pprofFilePath = fs::path(_pprofOutputPath) / pprofFilename;

    return pprofFilePath.string();
}

void LibddprofExporter::ExportToDisk(const std::string& applicationName, SerializedProfile const& encodedProfile, int32_t idx)
{
    auto pprofFilePath = GeneratePprofFilePath(applicationName, idx);

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

ddprof_ffi_Request* LibddprofExporter::CreateRequest(SerializedProfile const& encodedProfile, ddprof_ffi_ProfileExporterV3* exporter, const Tags& additionalTags) const
{
    auto start = encodedProfile.GetStart();
    auto end = encodedProfile.GetEnd();
    auto buffer = encodedProfile.GetBuffer();

    ddprof_ffi_File file{FfiHelper::StringToCharSlice(RequestFileName), ddprof_ffi_Vec_u8_as_slice(&buffer)};

    struct ddprof_ffi_Slice_file files
    {
        &file, 1
    };

    return ddprof_ffi_ProfileExporterV3_build(exporter, start, end, files, additionalTags.GetFfiTags(), RequestTimeOutMs);
}

bool LibddprofExporter::Send(ddprof_ffi_Request* request, ddprof_ffi_ProfileExporterV3* exporter) const
{
    assert(request != nullptr);

    auto result = ddprof_ffi_ProfileExporterV3_send(exporter, request, nullptr);

    on_leave { ddprof_ffi_SendResult_drop(result); };

    if (result.tag == DDPROF_FFI_SEND_RESULT_ERR)
    {
        Log::Error("Failed to send profile (", std::string(reinterpret_cast<const char*>(result.err.ptr), result.err.len), ")"); // NOLINT
        return false;
    }

    // Although we expect only 200, this range represents successful sends
    auto isSuccess = result.http_response.code >= 200 && result.http_response.code < 300;
    Log::Info("The profile was sent. Success?", std::boolalpha, isSuccess, std::noboolalpha, ", Http code: ", result.http_response.code);


    return isSuccess;
}

fs::path LibddprofExporter::CreatePprofOutputPath(IConfiguration* configuration) const
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
LibddprofExporter::SerializedProfile::SerializedProfile(ddprof_ffi_Profile* profile) :
    _encodedProfile{ddprof_ffi_Profile_serialize(profile, nullptr, nullptr)}
{
}

bool LibddprofExporter::SerializedProfile::IsValid() const
{
    return _encodedProfile.tag == DDPROF_FFI_SERIALIZE_RESULT_OK;
}

LibddprofExporter::SerializedProfile::~SerializedProfile()
{
    ddprof_ffi_SerializeResult_drop(_encodedProfile);
}

ddprof_ffi_Vec_u8 LibddprofExporter::SerializedProfile::GetBuffer() const
{
    return _encodedProfile.ok.buffer;
}

ddprof_ffi_Timespec LibddprofExporter::SerializedProfile::GetStart() const
{
    return _encodedProfile.ok.start;
}

ddprof_ffi_Timespec LibddprofExporter::SerializedProfile::GetEnd() const
{
    return _encodedProfile.ok.end;
}

//
// LibddprofExporter::Tags class
//

LibddprofExporter::Tags::Tags() :
    _ffiTags{ddprof_ffi_Vec_tag_new()}
{
}

LibddprofExporter::Tags::~Tags() noexcept
{
    ddprof_ffi_Vec_tag_drop(_ffiTags);
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

    auto pushResult = ddprof_ffi_Vec_tag_push(&_ffiTags, ffiName, ffiValue);
    if (pushResult.tag == DDPROF_FFI_PUSH_TAG_RESULT_ERR)
    {
        auto err_details = pushResult.err;
        Log::Debug(err_details.ptr);
    }
    ddprof_ffi_PushTagResult_drop(pushResult);
}

const ddprof_ffi_Vec_tag* LibddprofExporter::Tags::GetFfiTags() const
{
    return &_ffiTags;
}

//
// LibddprofExporter::ProfileAutoDelete class
//

LibddprofExporter::ProfileAutoDelete::ProfileAutoDelete(struct ddprof_ffi_Profile* profile) :
    _profile{profile}
{
}

LibddprofExporter::ProfileAutoDelete::~ProfileAutoDelete()
{
    ddprof_ffi_Profile_free(_profile);
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
