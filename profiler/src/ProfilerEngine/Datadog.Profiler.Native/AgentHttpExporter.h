// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EncodedPprof.h"
#include "Success.h"
#include "TagsHelper.h"

#include "shared/src/native-src/dd_filesystem.hpp"

#include <cstdint>
#include <memory>
#include <string>
#include <utility>
#include <vector>

class DebugPprofWriter;

// In-house replacement for the libdatadog Exporter + AgentProxy. Builds the
// profiling upload request (event.json v4 contract + multipart/form-data body)
// and sends it to the Datadog agent over plain TCP HTTP via HttpClient.
//
// No compression is applied: raw pprof bytes go on the wire (the intake sniffs
// the magic bytes and accepts uncompressed payloads).
class AgentHttpExporter
{
public:
    AgentHttpExporter(
        std::string host,
        int port,
        std::string path,
        std::string libraryName,
        std::string libraryVersion,
        std::string family,
        tags fixedTags,
        fs::path outputDirectory,
        int timeoutMs);

    ~AgentHttpExporter();

    libdatadog::Success Send(
        EncodedPprof& profile,
        std::string const& serviceName,
        tags additionalTags,
        std::vector<std::pair<std::string, std::vector<uint8_t>>> files,
        std::string metadata,
        std::string info,
        std::string processTags);

private:
    std::string BuildEventJson(
        EncodedPprof const& profile,
        tags const& additionalTags,
        std::vector<std::pair<std::string, std::vector<uint8_t>>> const& files,
        std::string const& metadata,
        std::string const& info,
        std::string const& processTags) const;

    static std::string GenerateBoundary();

    std::string _host;
    int _port;
    std::string _path;
    std::string _libraryName;
    std::string _libraryVersion;
    std::string _family;
    tags _fixedTags;
    int _timeoutMs;
    std::unique_ptr<DebugPprofWriter> _debugWriter;
};
