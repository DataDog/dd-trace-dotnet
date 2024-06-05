// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "SkipProfileHeuristicType.h"

#include <memory>
#include <string>

class IConfiguration;
class ISsiManager;

namespace libdatadog {

class TelemetryMetricsWorker
{
public:
    TelemetryMetricsWorker(ISsiManager* ssiManager);
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

    void IncNumberOfProfiles(bool hasSentProfiles);
    void IncNumberOfApplications();


private:
    static std::string TelemetryMetricsEndPoint;
    std::string _serviceName;
    bool _hasSentProfiles;
    ISsiManager* _ssiManager;

    struct Impl;
    std::unique_ptr<Impl> _impl;

    struct Key;
    void AddPoint(Key* key, double value, bool hasSentProfiles, SkipProfileHeuristicType heuristic);
};

} // namespace libdatadog
