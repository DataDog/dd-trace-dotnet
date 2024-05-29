// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IProfilerTelemetry.h"

#include <memory>
#include <string>

class IConfiguration;

namespace libdatadog {

class TelemetryMetricsWorker
{
public:
    TelemetryMetricsWorker();
    ~TelemetryMetricsWorker();

    bool Start(
        const IConfiguration* pConfiguration,
        const std::string& serviceName,
        const std::string& serviceVersion,
        const std::string& language,
        const std::string& languageVersion,
        const std::string& libraryVersion,
        const std::string& agentUrl,
        const std::string& runtimeId,
        const std::string& environment);
    void Stop();
    bool AddPoint(double value, bool hasSentProfiles, SkipProfileHeuristicType heuristic);

private:
    std::string _serviceName;

    struct Impl;
    std::unique_ptr<Impl> _impl;
};

} // namespace libdatadog
