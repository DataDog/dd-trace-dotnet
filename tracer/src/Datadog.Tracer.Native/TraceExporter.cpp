#include "TraceExporter.h"

#include "datadog/data-pipeline.h"

#include "logger.h"

namespace trace
{

class TraceExporter::Impl
{
public:
    Impl(ddog_TraceExporter* traceExporter) : Exporter{traceExporter}
    {
    }

    ~Impl()
    {
        if (Exporter != nullptr)
        {
            ddog_trace_exporter_free(Exporter);
        }
    }

    Impl(Impl const&) = delete;
    Impl& operator=(Impl const&) = delete;

    Impl(Impl&& other) noexcept
    {
        *this = std::move(other);
    }

    Impl& operator=(Impl&& other) noexcept
    {
        if (this == &other)
        {
            return *this;
        }

        Exporter = std::exchange(other.Exporter, nullptr);
    }

    ddog_TraceExporter* Exporter = nullptr;
};

TraceExporter::TraceExporter() = default;
TraceExporter::~TraceExporter() = default;

void TraceExporter::Initialize(std::string const& host, std::uint16_t port, std::string const& tracer_version,
                               std::string const& language, std::string const& language_version,
                               std::string const& language_interpreter)
{
    auto* traceExporter = ddog_trace_exporter_new(
        {.ptr = host.data(), .len = host.size()},
        port,
        {.ptr = tracer_version.data(), .len = tracer_version.size()},
        {.ptr = language.data(), .len = language.size()},
        {.ptr = language_version.data(), .len = language_version.size()},
        {.ptr = language_interpreter.data(), .len = language_interpreter.size()});

    if (traceExporter == nullptr)
    {
        Logger::Info("Failed to initialize TraceExporter.");
        return;
    }

    _impl = std::make_unique<Impl>(traceExporter);
}

std::string TraceExporter::Send(std::uint8_t* buffer, std::uintptr_t buffer_size, std::uintptr_t trace_count) const
{
    if (_impl == nullptr)
    {
        // TODO log
        return "Cannot send trace. TraceExporter is not correctly initialize.";
    }

    return ddog_trace_exporter_send(_impl->Exporter, {.ptr = buffer, .len = buffer_size}, trace_count);
}

} // namespace trace