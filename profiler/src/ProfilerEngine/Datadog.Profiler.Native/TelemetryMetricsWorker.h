// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "IProfilerTelemetry.h"

extern "C"
{
#include "datadog/common.h"
#include "datadog/telemetry.h"
}


class IConfiguration;

namespace libdatadog {

class TelemetryMetricsWorker
{
public:
    TelemetryMetricsWorker();
    ~TelemetryMetricsWorker();

    bool Start(
        IConfiguration* pConfiguration,
        const std::string& serviceName,
        const std::string& serviceVersion,
        const std::string& language,
        const std::string& languageVersion,
        const std::string& libraryVersion,
        const std::string& agentUrl,
        const std::string& runtimeId,
        const std::string& environment
        );
    bool AddPoint(double value, bool hasSentProfiles, SkipProfileHeuristicType heuristic);
    void Stop();

private:
    std::string _serviceName;
    ddog_TelemetryWorkerHandle* _pHandle;
    ddog_ContextKey _numberOfProfilesKey;
};

} // namespace libdatadog

