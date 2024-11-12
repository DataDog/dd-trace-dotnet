#pragma once

#include <memory>
#include <string>

#include "Success.h"

namespace libdatadog {
struct TagsImpl;
class AgentProxy;

class Tags
{
public:
    Tags(bool releaseOnClose = true);
    Tags(std::initializer_list<std::pair<std::string, std::string>> tags, bool releaseOnClose = true);
    ~Tags();

    Tags(Tags const&) = delete;
    Tags& operator=(Tags const&) = delete;
    Tags(Tags&& tags) noexcept;
    Tags& operator=(Tags&& tags) noexcept;

    Success Add(std::string const& name, std::string const& value);

private:
    friend class ExporterBuilder; // due to the libdatadog design, we need to access the implementation of the tags
    friend class AgentProxy;
    friend class TelemetryMetricsWorker;
    friend class ProfilerTelemetry;
    std::unique_ptr<TagsImpl> _impl;
};
} // namespace libdatadog
