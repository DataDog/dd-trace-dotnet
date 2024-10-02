#pragma once

#include <memory>
#include <string_view>

namespace trace
{

class TraceExporter
{
public:
    TraceExporter();
    ~TraceExporter();

    bool Initialize(std::string_view const& host, std::string_view const& tracer_version,
                                 std::string_view const& language, std::string_view const& language_version,
                                 std::string_view const& language_interpreter, std::string_view const& url,
                                 std::string_view const& env, std::string_view const& version,
                                 std::string_view const& service, bool compute_stats);

    bool Send(std::uint8_t* buffer, std::uintptr_t buffer_size, std::uintptr_t trace_count) const;

private:
    class Impl;
    std::unique_ptr<Impl> _impl;
};
} // namespace trace
