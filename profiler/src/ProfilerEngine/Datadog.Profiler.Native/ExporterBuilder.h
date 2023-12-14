// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "Tags.h"

#include "shared/src/native-src/dd_filesystem.hpp"

namespace libdatadog {

class Exporter;

class ExporterBuilder
{
public:
    ExporterBuilder();
    ~ExporterBuilder();

    ExporterBuilder& WithAgent(std::string url);
    ExporterBuilder& WithoutAgent(std::string site, std::string apiKey);
    ExporterBuilder& SetTags(Tags tags);
    ExporterBuilder& SetLibraryName(std::string s);
    ExporterBuilder& SetLibraryVersion(std::string s);
    ExporterBuilder& SetLanguageFamily(std::string s);
    ExporterBuilder& SetOutputDirectory(fs::path outputDirectory);
    std::unique_ptr<Exporter> Build();

private:
    std::unique_ptr<AgentProxy> CreateAgentProxy();

    struct AgentEndpoint;
    AgentEndpoint CreateEndpoint();

    std::string _site;
    std::string _apiKey;
    std::string _url;
    //
    std::string _libraryName;
    std::string _libraryVersion;
    std::string _languageFamily;
    Tags _tags;
    fs::path _outputDirectory;
};
} // namespace libdatadog
