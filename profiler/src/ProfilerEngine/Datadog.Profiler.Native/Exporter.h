// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <functional>
#include <memory>
#include <vector>

#include "Tags.h"
#include "error_code.h"
#include "shared/src/native-src/dd_filesystem.hpp"

namespace libdatadog {

namespace detail {
    struct ExporterImpl;
    class AgentExporter;
    class FileExporter;
} // namespace detail

class Profile;

class Exporter
{
private:
    Exporter(std::unique_ptr<detail::AgentExporter> ddExporter, std::unique_ptr<detail::FileExporter> fileExporter);

public:
    ~Exporter();

private:
    std::unique_ptr<detail::AgentExporter> _exporterImpl;
    std::unique_ptr<detail::FileExporter> _fileExporter;

public:
    error_code Send(Profile* r, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata);

public:

    class FileExporterBuilder
    {
    public:
        FileExporterBuilder() = default;
    };

    class ExporterBuilder
    {
    public:
        ExporterBuilder();
        ~ExporterBuilder();
        ExporterBuilder& WithAgent(std::string url);
        ExporterBuilder& WithoutAgent(std::string site, std::string apiKey);
        ExporterBuilder& WithTags(Tags tags);
        ExporterBuilder& SetLibraryName(std::string s);
        ExporterBuilder& SetLibraryVersion(std::string s);
        ExporterBuilder& SetLanguageFamily(std::string s);
        ExporterBuilder& WithFileExporter(fs::path outputDirectory);
        std::unique_ptr<Exporter> Build();

    private:
        std::unique_ptr<detail::AgentExporter> CreateDatadogAgentExporter();

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
};
} // namespace libdatadog
