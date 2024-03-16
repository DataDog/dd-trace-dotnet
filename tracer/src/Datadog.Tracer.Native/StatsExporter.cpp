#include "StatsExporter.h"

#include "datadog/data-pipeline.h"

#include "logger.h"

namespace trace
{

struct StatsExporter::Impl
{
    Impl(ddog_StatsExporter* exporter) : Exporter{exporter}
    {
    }

    ~Impl()
    {
        auto* exporter = std::exchange(Exporter, nullptr);
        if (exporter != nullptr)
        {
            ddog_stats_exporter_drop(exporter);
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
        return *this;
    }

    ddog_StatsExporter* Exporter;
};

StatsExporter::StatsExporter() = default;
StatsExporter::~StatsExporter() = default;

void StatsExporter::Initialize(std::string_view hostname, std::string_view env, std::string_view version,
                               std::string_view lang, std::string_view tracerVersion, std::string_view runtimeId,
                               std::string_view service, std::string_view containerId, std::string_view gitCommitSha,
                               std::vector<std::string_view> const& tags, std::string_view agentUrl)
{
    if (!CanUseExporter())
    {
        Logger::Info("Stats expoter initialization can only be done once");
        return;
    }

    _impl = std::make_unique<Impl>(nullptr);

    auto ffi_tags = ddog_Vec_Tag_new();
    // TODO for now, we consider tags to be empty. Needs clarification on the format

    auto result = ddog_stats_exporter_new(
        {.ptr = hostname.data(), .len = hostname.size()}, {.ptr = env.data(), .len = env.size()},
        {.ptr = version.data(), .len = version.size()}, {.ptr = lang.data(), .len = lang.size()},
        {.ptr = tracerVersion.data(), .len = tracerVersion.size()}, {.ptr = runtimeId.data(), .len = runtimeId.size()},
        {.ptr = service.data(), .len = service.size()}, {.ptr = containerId.data(), .len = containerId.size()},
        {.ptr = gitCommitSha.data(), .len = gitCommitSha.size()}, ffi_tags,
        {.ptr = agentUrl.data(), .len = agentUrl.size()}, &(_impl->Exporter));

    if (result.tag == DDOG_OPTION_ERROR_SOME_ERROR)
    {
        auto message = ddog_Error_message(&result.some);
        Logger::Info("Failed to create Stats Expoter: ", std::string_view(message.ptr, message.len));
        ddog_Error_drop(&result.some);
    }
}

void StatsExporter::AddSpanToBucket(std::string_view resourceName, std::string_view serviceName,
                                    std::string_view operationName, std::string_view spanType,
                                    std::int32_t httpStatusCode, bool isSyntheticsRequest, bool isTopLevel,
                                    bool isError, std::int64_t duration)
{
    if (!CanUseExporter())
    {
        Logger::Debug("Expoter not correctly initialized. Call Initialize first");
        return;
    }

    ddog_stats_exporter_insert_span_data(_impl->Exporter, {.ptr = resourceName.data(), .len = resourceName.size()},
                                         {.ptr = serviceName.data(), .len = serviceName.size()},
                                         {.ptr = operationName.data(), .len = operationName.size()},
                                         {.ptr = spanType.data(), .len = spanType.size()}, httpStatusCode,
                                         isSyntheticsRequest, isTopLevel, isError, duration);
}

void StatsExporter::Flush()
{
    if (!CanUseExporter())
    {
        Logger::Debug("Expoter not correctly initialized. Call Initialize first");
        return;
    }

    ddog_stats_exporter_send(_impl->Exporter);
}

bool StatsExporter::CanUseExporter() const
{
    return _impl != nullptr && _impl->Exporter == nullptr;
}

} // namespace trace