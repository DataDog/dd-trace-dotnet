// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <functional>
#include <memory>
#include <vector>

#include "Success.h"
#include "Tags.h"

namespace libdatadog {

class AgentProxy;
class FileSaver;

class Profile;

class Exporter
{
private:
    friend class ExporterBuilder;
    Exporter(std::unique_ptr<AgentProxy> agentProxy, std::unique_ptr<FileSaver> fileSaver);

public:
    ~Exporter();

private:
    std::unique_ptr<AgentProxy> _agentProxy;
    std::unique_ptr<FileSaver> _fileSaver;

public:
    Success Send(Profile* r, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata, std::string info);
};
} // namespace libdatadog
