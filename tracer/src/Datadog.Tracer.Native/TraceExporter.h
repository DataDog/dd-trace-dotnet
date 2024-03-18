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

    bool Initialize(std::string_view const& host, std::uint16_t port, std::string_view const& tracer_version,
                    std::string_view const& language, std::string_view const& language_version,
                    std::string_view const& language_interpreter);

    std::string Send(std::uint8_t* buffer, std::uintptr_t buffer_size, std::uintptr_t trace_count) const;

private:
    class Impl;
    std::unique_ptr<Impl> _impl;
};
} // namespace trace
