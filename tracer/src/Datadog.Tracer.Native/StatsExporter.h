#pragma once

#include <memory>
#include <string>
#include <string_view>
#include <vector>

namespace trace
{
class StatsExporter
{
public:
    StatsExporter();
    ~StatsExporter();

    void Initialize(std::string_view hostname, std::string_view env, std::string_view version, std::string_view lang,
                    std::string_view tracerVersion, std::string_view runtimeId, std::string_view service,
                    std::string_view containerId, std::string_view gitCommitSha,
                    std::vector<std::string_view> const& tags, std::string_view agentUrl);

    void AddSpanToBucket(std::string_view resourceName, std::string_view serviceName, std::string_view operationName,
                         std::string_view spanType, std::int32_t httpStatusCode, bool isSyntheticsRequest,
                         bool isTopLevel, bool isError, std::int64_t duration);

    void Flush();

private:
    bool CanUseExporter() const;

    struct Impl;
    std::unique_ptr<Impl> _impl;
};

} // namespace trace
