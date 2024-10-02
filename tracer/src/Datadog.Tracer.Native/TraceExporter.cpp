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

        Exporter = std::exchange(other.Exporter, Exporter);
    }

    ddog_TraceExporter* Exporter = nullptr;
};

TraceExporter::TraceExporter() = default;
TraceExporter::~TraceExporter() = default;

bool TraceExporter::Initialize(std::string_view const& host, std::string_view const& tracer_version,
                               std::string_view const& language, std::string_view const& language_version,
                               std::string_view const& language_interpreter, std::string_view const& url,
                               std::string_view const& env, std::string_view const& version,
                               std::string_view const& service, const bool compute_stats)
{
    ddog_TraceExporter** exporter = nullptr; //todo: do we need to alloc here?

    auto maybeError = ddog_trace_exporter_new(
        exporter,  {.ptr = url.data(), .len = url.size()},
        {.ptr = tracer_version.data(), .len = tracer_version.size()},
        {.ptr = language.data(), .len = language.size()},
        {.ptr = language_version.data(), .len = language_version.size()},
        {.ptr = language_interpreter.data(), .len = language_interpreter.size()},
        {.ptr = host.data(), .len = host.size()},
        {.ptr = env.data(), .len = env.size()},
        {.ptr = version.data(), .len = version.size()},
    {.ptr = service.data(), .len = service.size()},
        DDOG_TRACE_EXPORTER_INPUT_FORMAT_V04,
        DDOG_TRACE_EXPORTER_OUTPUT_FORMAT_V04,
        compute_stats, nullptr); //todo: callback?

    if (maybeError.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        Logger::Info("Failed to initialize TraceExporter.");
        return false;
    }

    _impl = std::make_unique<Impl>(*exporter); //TODO: is this ok?
    return true;
}

bool TraceExporter::Send(std::uint8_t* buffer, std::uintptr_t buffer_size, std::uintptr_t trace_count) const
{
    if (_impl == nullptr)
    {
        // TODO log
        return "Cannot send trace. TraceExporter is not correctly initialize.";
    }

    struct free_char
    {
        void operator()(void* s)
        {
            free(s);
        }
    };

    const auto result = ddog_trace_exporter_send(_impl->Exporter, {.ptr = buffer, .len = buffer_size}, trace_count);

    return result.tag == DDOG_OPTION_ERROR_NONE_ERROR;
}

} // namespace trace