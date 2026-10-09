// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AgentHttpExporter.h"

#include "DebugPprofWriter.h"
#include "FfiHelper.h"
#include "HttpClient.h"
#include "Log.h"
#include "OpSysTools.h"

#include <chrono>
#include <cstdio>
#include <ctime>
#include <sstream>

namespace {
// The profile is uploaded as a multipart part named "auto.pprof" (matches the
// libdatadog convention and the integration-test MultiPartReader expectations).
constexpr const char* ProfileFilename = "auto.pprof";
constexpr const char* EventFilename = "event.json";
constexpr const char* EventContract = "4";

void AppendJsonEscaped(std::string& out, std::string_view value)
{
    for (char c : value)
    {
        switch (c)
        {
            case '"':
                out += "\\\"";
                break;
            case '\\':
                out += "\\\\";
                break;
            case '\b':
                out += "\\b";
                break;
            case '\f':
                out += "\\f";
                break;
            case '\n':
                out += "\\n";
                break;
            case '\r':
                out += "\\r";
                break;
            case '\t':
                out += "\\t";
                break;
            default:
                if (static_cast<unsigned char>(c) < 0x20)
                {
                    char buffer[8];
                    std::snprintf(buffer, sizeof(buffer), "\\u%04x", c);
                    out += buffer;
                }
                else
                {
                    out += c;
                }
                break;
        }
    }
}

std::string FormatRfc3339(std::chrono::system_clock::time_point tp)
{
    auto ns = std::chrono::duration_cast<std::chrono::nanoseconds>(tp.time_since_epoch()).count();
    auto seconds = static_cast<std::time_t>(ns / 1000000000);
    auto nanos = static_cast<long>(ns % 1000000000);

    struct tm buf = {};
#ifdef _WINDOWS
    gmtime_s(&buf, &seconds);
#else
    gmtime_r(&seconds, &buf);
#endif

    char timeBuffer[32];
    std::strftime(timeBuffer, sizeof(timeBuffer), "%Y-%m-%dT%H:%M:%S", &buf);

    char result[48];
    std::snprintf(result, sizeof(result), "%s.%09ldZ", timeBuffer, nanos);
    return result;
}
} // namespace

AgentHttpExporter::AgentHttpExporter(
    std::string host,
    int port,
    std::string path,
    std::string libraryName,
    std::string libraryVersion,
    std::string family,
    tags fixedTags,
    fs::path outputDirectory,
    int timeoutMs) :
    _host{std::move(host)},
    _port{port},
    _path{std::move(path)},
    _libraryName{std::move(libraryName)},
    _libraryVersion{std::move(libraryVersion)},
    _family{std::move(family)},
    _fixedTags{std::move(fixedTags)},
    _timeoutMs{timeoutMs}
{
    if (!outputDirectory.empty())
    {
        _debugWriter = std::make_unique<DebugPprofWriter>(std::move(outputDirectory));
    }
}

AgentHttpExporter::~AgentHttpExporter() = default;

std::string AgentHttpExporter::GenerateBoundary()
{
    std::stringstream ss;
    ss << "--------------------------" << std::hex << OpSysTools::GetHighPrecisionNanoseconds();
    return ss.str();
}

std::string AgentHttpExporter::BuildEventJson(
    EncodedPprof const& profile,
    tags const& additionalTags,
    std::vector<std::pair<std::string, std::vector<uint8_t>>> const& files,
    std::string const& metadata,
    std::string const& info,
    std::string const& processTags) const
{
    std::string json;
    json.reserve(1024);

    json += "{";

    // attachments
    json += "\"attachments\":[\"";
    AppendJsonEscaped(json, ProfileFilename);
    json += "\"";
    for (auto const& [filename, _] : files)
    {
        json += ",\"";
        AppendJsonEscaped(json, filename);
        json += "\"";
    }
    json += "]";

    // tags_profiler (comma-joined key:value pairs)
    std::string joinedTags;
    bool first = true;
    auto appendTag = [&joinedTags, &first](std::string const& name, std::string const& value) {
        if (!first)
        {
            joinedTags += ",";
        }
        joinedTags += name;
        joinedTags += ":";
        joinedTags += value;
        first = false;
    };
    for (auto const& [name, value] : _fixedTags)
    {
        appendTag(name, value);
    }
    for (auto const& [name, value] : additionalTags)
    {
        appendTag(name, value);
    }

    json += ",\"tags_profiler\":\"";
    AppendJsonEscaped(json, joinedTags);
    json += "\"";

    // collection window
    json += ",\"start\":\"";
    json += FormatRfc3339(profile.Start);
    json += "\"";
    json += ",\"end\":\"";
    json += FormatRfc3339(profile.End);
    json += "\"";

    // family / version
    json += ",\"family\":\"";
    AppendJsonEscaped(json, _family);
    json += "\"";
    json += ",\"version\":\"";
    json += EventContract;
    json += "\"";

    // endpoint counts (only when present)
    if (!profile.EndpointCounts.empty())
    {
        json += ",\"endpoint_counts\":{";
        bool firstEndpoint = true;
        for (auto const& [endpoint, count] : profile.EndpointCounts)
        {
            if (!firstEndpoint)
            {
                json += ",";
            }
            json += "\"";
            AppendJsonEscaped(json, endpoint);
            json += "\":";
            json += std::to_string(count);
            firstEndpoint = false;
        }
        json += "}";
    }

    // process tags (only when present)
    if (!processTags.empty())
    {
        json += ",\"process_tags\":\"";
        AppendJsonEscaped(json, processTags);
        json += "\"";
    }

    // internal metadata (always present; the systemInfo object or {})
    json += ",\"internal\":";
    json += metadata.empty() ? "{}" : metadata;

    // info (only when present; raw JSON object)
    if (!info.empty())
    {
        json += ",\"info\":";
        json += info;
    }

    json += "}";
    return json;
}

libdatadog::Success AgentHttpExporter::Send(
    EncodedPprof& profile,
    std::string const& serviceName,
    tags additionalTags,
    std::vector<std::pair<std::string, std::vector<uint8_t>>> files,
    std::string metadata,
    std::string info,
    std::string processTags)
{
    if (_debugWriter != nullptr)
    {
        auto success = _debugWriter->WriteToDisk(profile, serviceName, files, metadata, info);
        if (!success)
        {
            Log::Error(success.message());
        }
    }

    auto eventJson = BuildEventJson(profile, additionalTags, files, metadata, info, processTags);
    auto boundary = GenerateBoundary();

    // Assemble the multipart/form-data body.
    std::vector<uint8_t> body;
    body.reserve(profile.Bytes.size() + eventJson.size() + 1024);

    auto appendString = [&body](std::string_view s) {
        body.insert(body.end(), s.begin(), s.end());
    };
    auto appendBytes = [&body](std::vector<uint8_t> const& b) {
        body.insert(body.end(), b.begin(), b.end());
    };
    auto appendPartHeader = [&](std::string const& name, std::string const& filename, std::string const& contentType) {
        appendString("--");
        appendString(boundary);
        appendString("\r\n");
        appendString("Content-Disposition: form-data; name=\"");
        appendString(name);
        appendString("\"; filename=\"");
        appendString(filename);
        appendString("\"\r\n");
        appendString("Content-Type: ");
        appendString(contentType);
        appendString("\r\n\r\n");
    };

    // event.json part
    appendPartHeader("event", EventFilename, "application/json");
    appendString(eventJson);
    appendString("\r\n");

    // profile.pprof part (raw, uncompressed)
    appendPartHeader(ProfileFilename, ProfileFilename, "application/octet-stream");
    appendBytes(profile.Bytes);
    appendString("\r\n");

    // extra files (raw, uncompressed)
    for (auto const& [filename, content] : files)
    {
        appendPartHeader(filename, filename, "application/octet-stream");
        appendBytes(content);
        appendString("\r\n");
    }

    appendString("--");
    appendString(boundary);
    appendString("--\r\n");

    std::vector<HttpClient::Header> headers = {
        {"DD-EVP-ORIGIN", _libraryName},
        {"DD-EVP-ORIGIN-VERSION", _libraryVersion},
        {"User-Agent", _libraryName + "/" + _libraryVersion},
        {"Content-Type", "multipart/form-data; boundary=" + boundary},
    };

    auto response = HttpClient::Post(_host, _port, _path, headers, body, _timeoutMs);
    if (!response.Succeeded)
    {
        return libdatadog::make_error(response.Error);
    }

    if (response.StatusCode < 200 || response.StatusCode >= 300)
    {
        return libdatadog::make_error("Agent returned HTTP status " + std::to_string(response.StatusCode));
    }

    return libdatadog::make_success();
}
