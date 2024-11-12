// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ExporterBuilder.h"

#include "AgentProxy.hpp"
#include "Exception.h"
#include "Exporter.h"
#include "FfiHelper.h"
#include "FileSaver.hpp"
#include "SuccessImpl.hpp"
#include "Tags.h"
#include "TagsImpl.hpp"

#include <assert.h>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

ExporterBuilder::ExporterBuilder() = default;
ExporterBuilder::~ExporterBuilder() = default;

ExporterBuilder& ExporterBuilder::WithAgent(std::string url)
{
    assert(_site.empty());
    assert(_apiKey.empty());
    _url = std::move(url);
    return *this;
}

ExporterBuilder& ExporterBuilder::WithoutAgent(std::string site, std::string apiKey)
{
    assert(_url.empty());

    _site = std::move(site);
    _apiKey = std::move(apiKey);
    return *this;
}

ExporterBuilder& ExporterBuilder::SetTags(Tags tags)
{
    _tags = std::move(tags);
    return *this;
}

struct ExporterBuilder::AgentEndpoint
{
    ddog_prof_Endpoint inner;
};

std::unique_ptr<libdatadog::AgentProxy> ExporterBuilder::CreateAgentProxy()
{
    auto endpoint = CreateEndpoint();

    auto result = ddog_prof_Exporter_new(
        to_char_slice(_libraryName),
        to_char_slice(_libraryVersion),
        to_char_slice(_languageFamily),
        static_cast<ddog_Vec_Tag*>(*_tags._impl),
        endpoint.inner);

    if (result.tag == DDOG_PROF_EXPORTER_NEW_RESULT_ERR)
    {
        throw Exception(std::make_unique<SuccessImpl>(result.err));
    }

    // the AgentProxy instance is acquiring the ownership of the ddog_prof_Exporter pointer stored in the ok field
    return std::make_unique<AgentProxy>(result.ok);
}

ExporterBuilder& ExporterBuilder::SetOutputDirectory(fs::path outputDirectory)
{
    _outputDirectory = std::move(outputDirectory);
    return *this;
}

ExporterBuilder& ExporterBuilder::SetLibraryName(std::string libraryName)
{
    _libraryName = std::move(libraryName);
    return *this;
}

ExporterBuilder& ExporterBuilder::SetLibraryVersion(std::string libraryVersion)
{
    _libraryVersion = std::move(libraryVersion);
    return *this;
}

ExporterBuilder& ExporterBuilder::SetLanguageFamily(std::string family)
{
    _languageFamily = std::move(family);
    return *this;
}

ExporterBuilder::AgentEndpoint ExporterBuilder::CreateEndpoint()
{
    if (_url.empty())
    {
        assert(!_site.empty());
        assert(!_apiKey.empty());
        return {ddog_prof_Endpoint_agentless(to_char_slice(_site), to_char_slice(_apiKey))};
    }

    return {ddog_prof_Endpoint_agent(to_char_slice(_url))};
}

std::unique_ptr<Exporter> ExporterBuilder::Build()
{
    auto agentProxy = CreateAgentProxy();

    std::unique_ptr<FileSaver> fileSaver = nullptr;
    if (!_outputDirectory.empty())
    {
        fileSaver = std::make_unique<FileSaver>(_outputDirectory);
    }

    return std::unique_ptr<Exporter>(new Exporter(std::move(agentProxy), std::move(fileSaver)));
}
} // namespace libdatadog