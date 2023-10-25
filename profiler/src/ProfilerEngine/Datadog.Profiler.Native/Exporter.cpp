// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Exporter.h"

#include "EncodedProfile.hpp"
#include "Exception.h"
#include "FfiHelper.h"
#include "Log.h"
#include "Profile.h"
#include "Tags.h"
#include "libdatadog_details/AgentExporter.hpp"
#include "libdatadog_details/Exporter.hpp"
#include "libdatadog_details/FileExporter.hpp"
#include "libdatadog_details/Profile.hpp"
#include "libdatadog_details/Tags.hpp"
#include "libdatadog_details/error_code.hpp"
#include "libdatadog_helper.hpp"
#include "std_extensions.hpp"

#include <cassert>

namespace libdatadog {

Exporter::Exporter(std::unique_ptr<detail::AgentExporter> ddExporter, std::unique_ptr<detail::FileExporter> fileExporter) :
    _exporterImpl{std::move(ddExporter)},
    _fileExporter{std::move(fileExporter)}
{
}

Exporter::~Exporter() = default;

Exporter::ExporterBuilder& Exporter::ExporterBuilder::WithAgent(std::string url)
{
    assert(!_agentless);
    _withAgent = true;
    _url = std::move(url);
    return *this;
}

Exporter::ExporterBuilder& Exporter::ExporterBuilder::WithoutAgent(std::string site, std::string apiKey)
{
    assert(!_withAgent);
    _agentless = true;

    _site = std::move(site);
    _apiKey = std::move(apiKey);
    return *this;
}

Exporter::ExporterBuilder& Exporter::ExporterBuilder::WithTags(Tags tags)
{
    _tags = std::move(tags);
    return *this;
}

struct Exporter::ExporterBuilder::AgentEndpoint
{
    ddog_Endpoint inner;
};

std::unique_ptr<libdatadog::detail::AgentExporter> Exporter::ExporterBuilder::CreateDatadogAgentExporter()
{
    auto endpoint = CreateEndpoint();

    auto result = ddog_prof_Exporter_new(
        FfiHelper::StringToCharSlice(_libraryName),
        FfiHelper::StringToCharSlice(_libraryVersion),
        FfiHelper::StringToCharSlice(_languageFamily),
        static_cast<ddog_Vec_Tag*>(*_tags._impl),
        endpoint.inner);

    if (result.tag == DDOG_PROF_EXPORTER_NEW_RESULT_ERR)
    {
        auto error = detail::make_error(result.err);
        throw Exception(error.message());
    }

    // the AgentExporter instance is acquiring the ownership of the ok ptr
    return std::make_unique<detail::AgentExporter>(result.ok);
}

Exporter::ExporterBuilder::ExporterBuilder() = default;
Exporter::ExporterBuilder::~ExporterBuilder() = default;

Exporter::ExporterBuilder& Exporter::ExporterBuilder::WithFileExporter(fs::path outputDirectory)
{
    _outputDirectory = std::move(outputDirectory);
    return *this;
}

Exporter::ExporterBuilder& Exporter::ExporterBuilder::SetLibraryName(std::string libraryName)
{
    _libraryName = std::move(libraryName);
    return *this;
}

Exporter::ExporterBuilder& Exporter::ExporterBuilder::SetLibraryVersion(std::string libraryVersion)
{
    _libraryVersion = std::move(libraryVersion);
    return *this;
}

Exporter::ExporterBuilder& Exporter::ExporterBuilder::SetLanguageFamily(std::string family)
{
    _languageFamily = std::move(family);
    return *this;
}

Exporter::ExporterBuilder::AgentEndpoint Exporter::ExporterBuilder::CreateEndpoint()
{
    if (_agentless)
    {
        assert(!_site.empty());
        assert(!_apiKey.empty());
        return {ddog_Endpoint_agentless(FfiHelper::StringToCharSlice(_site), FfiHelper::StringToCharSlice(_apiKey))};
    }

    assert(!_url.empty());
    return {ddog_Endpoint_agent(FfiHelper::StringToCharSlice(_url))};
}

std::unique_ptr<Exporter> Exporter::ExporterBuilder::Build()
{
    auto datadogAgentExporter = CreateDatadogAgentExporter();

    std::unique_ptr<detail::FileExporter> fileExporter = nullptr;
    if (!_outputDirectory.empty())
    {
        fileExporter = std::make_unique<detail::FileExporter>(_outputDirectory);
    }

    return std::unique_ptr<Exporter>(new Exporter(std::move(datadogAgentExporter), std::move(fileExporter)));
}

libdatadog::error_code Exporter::Send(Profile* profile, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata)
{
    auto s = ddog_prof_Profile_serialize(*(profile->_impl), nullptr, nullptr);

    if (s.tag == DDOG_PROF_PROFILE_SERIALIZE_RESULT_ERR)
    {
        return detail::make_error(s.err);
    }

    auto ep = EncodedProfile(&s.ok);

    if (_fileExporter != nullptr)
    {
        auto error_code = _fileExporter->WriteToDisk(ep, profile->GetApplicationName());
        if (!error_code)
        {
            Log::Error(error_code.message());
        }
    }

    assert(_exporterImpl != nullptr);
    return _exporterImpl->Send(ep, std::move(tags), std::move(files), std::move(metadata));
}

} // namespace libdatadog
