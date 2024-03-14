#pragma once

#include <memory>
#include <string>

namespace trace
{

class TraceExporter
{
public:
    TraceExporter();
    ~TraceExporter();

    bool Initialize(std::string const& host, std::uint16_t port, std::string const& tracer_version,
                    std::string const& language, std::string const& language_version,
                    std::string const& language_interpreter);

    std::string Send(std::uint8_t* buffer, std::uintptr_t buffer_size, std::uintptr_t trace_count) const;

private:
    class Impl;
    std::unique_ptr<Impl> _impl;
};
} // namespace trace
