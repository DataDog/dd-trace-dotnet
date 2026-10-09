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
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"
}

namespace libdatadog {
constexpr uint64_t EndpointTimeoutMs = 0; // 0 means "use the default timeout"
constexpr bool UseSystemResolver = true;

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
    ddog_prof_endpoint inner;
};

std::unique_ptr<libdatadog::AgentProxy> ExporterBuilder::CreateAgentProxy()
{
    auto endpoint = CreateEndpoint();

    ddog_prof_exporter* rawExporter = nullptr;
    auto result = ddog_prof_exporter_new(
        to_poc_char_slice(_libraryName),
        to_poc_char_slice(_libraryVersion),
        to_poc_char_slice(_languageFamily),
        static_cast<ddog_vec_tag*>(*_tags._impl),
        &endpoint.inner,
        &rawExporter);

    if (result != DDOG_OK)
    {
        throw Exception(std::make_unique<SuccessImpl>(result));
    }

    // the AgentProxy instance is acquiring the ownership of the ddog_prof_exporter pointer
    return std::make_unique<AgentProxy>(rawExporter);
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
    ddog_prof_endpoint endpoint;
    ddog_error_code rc;

    if (_url.empty())
    {
        assert(!_site.empty());
        assert(!_apiKey.empty());
        rc = ddog_prof_endpoint_agentless(to_poc_char_slice(_site), to_poc_char_slice(_apiKey), EndpointTimeoutMs, UseSystemResolver, &endpoint);
    }
    else
    {
        rc = ddog_prof_endpoint_agent(to_poc_char_slice(_url), EndpointTimeoutMs, UseSystemResolver, &endpoint);
    }

    if (rc != DDOG_OK)
    {
        throw Exception(std::make_unique<SuccessImpl>(rc));
    }

    return {endpoint};
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