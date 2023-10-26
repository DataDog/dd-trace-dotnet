#pragma once

#include <memory>
#include <string>

#include "ErrorCode.h"

namespace libdatadog {
namespace detail {
    struct TagsImpl;
    class AgentExporter;
}

class Tags
{
public:
    Tags();
    Tags(std::initializer_list<std::pair<std::string, std::string>> tags);
    ~Tags();

    Tags(Tags const&) = delete;
    Tags& operator=(Tags const&) = delete;
    Tags(Tags&& tags) noexcept;
    Tags& operator=(Tags&& tags) noexcept;

    ErrorCode Add(std::string const& name, std::string const& value);

private:
    friend class Exporter; // due to the libdatadog design, we need to access the implementation of the tags
    friend class detail::AgentExporter;
    std::unique_ptr<detail::TagsImpl> _impl;
};
} // namespace libdatadog
