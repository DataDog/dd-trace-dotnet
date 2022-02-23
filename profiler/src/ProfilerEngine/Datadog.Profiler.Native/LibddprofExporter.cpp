// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibddprofExporter.h"
#include "FfiHelper.h"
#include "Log.h"
#include "OpSysTools.h"
#include "Sample.h"

#include <cassert>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string.h>
#include <time.h>
#include <string.h>

#define BUFFER_MAX_SIZE 512

tags LibddprofExporter::CommonTags = {
    {"language", "dotnet"},
    {"profiler_version", "1.1.1"},
#ifdef BIT64
    {"process_architecture", "x64"},
#else
    {"process_architecture", "x86"}
#endif
};

// need to be static so it leave longer for the shared library
std::string const LibddprofExporter::ProcessId = std::to_string(OpSysTools::GetProcId());

int const LibddprofExporter::RequestTimeOutMs = 10000;

std::string const LibddprofExporter::LanguageFamily = "dotnet";

std::string const LibddprofExporter::RequestFileName = "auto.pprof";

std::string const LibddprofExporter::ProfilePeriodType = "RealTime";

std::string const LibddprofExporter::ProfilePeriodUnit = "Nanoseconds";

LibddprofExporter::LibddprofExporter(IConfiguration* configuration) :
    _locations{256},
    _lines{256}
{
    auto tags = CreateTags(configuration);

    auto endpoint = CreateEndpoint(configuration);

    _exporterImpl = CreateExporter(tags.GetFfiTags(), endpoint);
    _profile = CreateProfile();

    _pprofOutputPath = CreatePprofOutputPath(configuration);

    if (!_pprofOutputPath.empty())
    {
        Log::Info("Profiles will be exported on disk in ", _pprofOutputPath);
        _pprofFileNamePrefix = configuration->GetServiceName() + "_" + std::to_string(OpSysTools::GetProcId()) + "_";
    }
}

LibddprofExporter::~LibddprofExporter()
{
    ddprof_ffi_ProfileExporterV3_delete(_exporterImpl);
    ddprof_ffi_Profile_free(_profile);
}

ddprof_ffi_ProfileExporterV3* LibddprofExporter::CreateExporter(ddprof_ffi_Slice_tag tags, ddprof_ffi_EndpointV3 endpoint)
{
    auto result = ddprof_ffi_ProfileExporterV3_new(FfiHelper::StringToByteSlice(LanguageFamily), tags, endpoint);
    if (result.tag == DDPROF_FFI_NEW_PROFILE_EXPORTER_V3_RESULT_OK)
    {
        return result.ok;
    }
    else
    {
        Log::Error("libddprof failed to create the exporter: ", result.err.ptr);
        ddprof_ffi_Buffer_reset(&result.err);
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

    return ddprof_ffi_Profile_new(sample_types, &period);
}

LibddprofExporter::Tags LibddprofExporter::CreateTags(IConfiguration* configuration)
{
    auto tags = LibddprofExporter::Tags{};

    for (auto const& [name, value] : CommonTags)
    {
        tags.Add(name, value);
    }

    tags.Add("pid", ProcessId);
    tags.Add("host", configuration->GetHostname());
    tags.Add("service", configuration->GetServiceName());
    tags.Add("env", configuration->GetEnvironment());
    tags.Add("version", configuration->GetVersion());

    // TODO get
    // runtime_version
    // runtime_platform (os and version, archi)

    for (auto const& [name, value] : configuration->GetUserTags())
    {
        tags.Add(name, value);
    }

    return tags;
}

ddprof_ffi_EndpointV3 LibddprofExporter::CreateEndpoint(IConfiguration* configuration)
{
    if (configuration->IsAgentless())
    {
        // handle "agentless" case
        auto const& site = configuration->GetSite();
        auto const& apiKey = configuration->GetApiKey();

        return ddprof_ffi_EndpointV3_agentless(FfiHelper::StringToByteSlice(site), FfiHelper::StringToByteSlice(apiKey));
    }

    // handle "with agent" case
    auto const& url = configuration->GetAgentUrl();
    if (!url.empty())
    {
        _agentUrl = url;
    }
    else
    {
        // agent mode
        std::stringstream oss;
        oss << "http://" << configuration->GetAgentHost() << ":" << configuration->GetAgentPort();
        _agentUrl = oss.str();
    }

    return ddprof_ffi_EndpointV3_agent(FfiHelper::StringToByteSlice(_agentUrl));
}

void LibddprofExporter::Add(Sample const& sample)
{
    if (_exporterImpl == nullptr)
    {
        // TODO: to be throttled
        Log::Error("libddprof exporter was not successfully created. No sample cannot be added.");
    }

    auto const& callstack = sample.GetCallstack();

    auto nbFrames = 0UL;
    for (auto const& frame : sample.GetCallstack())
    {
        auto& line = _lines[nbFrames];
        auto& location = _locations[nbFrames];

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

        ++nbFrames;
    }

    auto ffiSample = ddprof_ffi_Sample{};
    ffiSample.locations = {_locations.data(), nbFrames};

    // Labels
    auto const& labels = sample.GetLabels();
    std::vector<ddprof_ffi_Label> ffiLabels{labels.size()};

    for (auto const& [label, value] : labels)
    {
        ffiLabels.push_back({{label.data(), label.size()}, {value.data(), value.size()}});
    }
    ffiSample.labels = {ffiLabels.data(), ffiLabels.size()};

    //values
    auto const& values = sample.GetValues();
    ffiSample.values = {values.data(), values.size()};

    ddprof_ffi_Profile_add(_profile, ffiSample);
}

void LibddprofExporter::Export()
{

    if (_exporterImpl == nullptr)
    {
        Log::Error("Libddprof exporter was not successfully create. No profile cannot be exported.");
        return;
    }

    auto profileAutoReset = ProfileAutoReset{_profile};

    auto serializedProfile = SerializedProfile{_profile};
    if (!serializedProfile.IsValid())
    {
        Log::Error("Unable to serialize the libddprof profile. No profile will be sent.");
        return;
    }

    if (!_pprofOutputPath.empty())
    {
        ExportToDisk(serializedProfile);
    }

    auto* request = CreateRequest(serializedProfile);

    if (request != nullptr)
    {
        Send(request);
    }
    else
    {
        Log::Error("Unable to create a request to send the profile.");
    }
}

std::string LibddprofExporter::GeneratePprofFilePath()
{
    auto time = std::time(nullptr);
    struct tm buf;

#ifdef _WINDOWS
    localtime_s(&buf, &time);
#else
    localtime_r(&time, &buf);
#endif

    std::stringstream oss;
    oss << _pprofFileNamePrefix << std::put_time(&buf, "%F_%H-%M-%S") << ".pprof";
    auto pprofFilename = oss.str();

    auto pprofFilePath = std::filesystem::path(_pprofOutputPath) / pprofFilename;

    return pprofFilePath.string();
}

void LibddprofExporter::ExportToDisk(SerializedProfile const& encodedProfile)
{
    auto pprofFilePath = GeneratePprofFilePath();

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

ddprof_ffi_Request* LibddprofExporter::CreateRequest(SerializedProfile const& encodedProfile) const
{
    auto start = encodedProfile.GetStart();
    auto end = encodedProfile.GetEnd();
    auto buffer = encodedProfile.GetBuffer();

    ddprof_ffi_File file{FfiHelper::StringToByteSlice(RequestFileName), &buffer};

    struct ddprof_ffi_Slice_file files
    {
        &file, 1
    };

    return ddprof_ffi_ProfileExporterV3_build(_exporterImpl, start, end, files, RequestTimeOutMs);
}
void LibddprofExporter::Send(ddprof_ffi_Request* request) const
{
    assert(request != nullptr);

    auto result = ddprof_ffi_ProfileExporterV3_send(_exporterImpl, request);
    if (result.tag == DDPROF_FFI_SEND_RESULT_FAILURE)
    {
        // There is an overflow issue when using the error buffer from rust
        // Log::Error("libddprof error: Failed to send profile (", result.failure.ptr, ")");
        Log::Error("libddprof error: Failed to send profile.");
        ddprof_ffi_Buffer_reset(&result.failure);
    }
    else
    {
        // Although we expect only 200, this range represents successful sends
        auto isSuccess = result.http_response.code >= 200 && result.http_response.code < 300;
        Log::Info("The profile was sent. Success?", std::boolalpha, isSuccess, std::noboolalpha, ", Http code: ", result.http_response.code);
    }
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
    if (std::filesystem::create_directories(pprofOutputPath, errorCode) || (errorCode.value() == 0))
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
    _encodedProfile{ddprof_ffi_Profile_serialize(profile)}
{
}

bool LibddprofExporter::SerializedProfile::IsValid() const
{
    return _encodedProfile != nullptr;
}

LibddprofExporter::SerializedProfile::~SerializedProfile()
{
    ddprof_ffi_EncodedProfile_delete(_encodedProfile);
}

ddprof_ffi_Buffer LibddprofExporter::SerializedProfile::GetBuffer() const
{
    return _encodedProfile->buffer;
}

ddprof_ffi_Timespec LibddprofExporter::SerializedProfile::GetStart() const
{
    return _encodedProfile->start;
}

ddprof_ffi_Timespec LibddprofExporter::SerializedProfile::GetEnd() const
{
    return _encodedProfile->end;
}

//
// LibddprofExporter::Tags class
//

void LibddprofExporter::Tags::Add(std::string const& labelName, std::string const& labelValue)
{
    _stringTags.push_front({labelName, labelValue});

    auto const& [name, value] = _stringTags.front();

    auto ffiName = FfiHelper::StringToByteSlice(name);
    auto ffiValue = FfiHelper::StringToByteSlice(value);

    _ffiTags.push_back({ffiName, ffiValue});
}

ddprof_ffi_Slice_tag LibddprofExporter::Tags::GetFfiTags() const
{
    return {_ffiTags.data(), _ffiTags.size()};
}

//
// LibddprofExporter::Profile class
//

LibddprofExporter::ProfileAutoReset::ProfileAutoReset(struct ddprof_ffi_Profile* profile) :
    _profile{profile}
{
}

LibddprofExporter::ProfileAutoReset::~ProfileAutoReset()
{
    ddprof_ffi_Profile_reset(_profile);
}