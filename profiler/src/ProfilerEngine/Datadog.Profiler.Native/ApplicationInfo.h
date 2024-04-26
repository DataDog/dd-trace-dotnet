#pragma once
#include <string>
#include <memory>

#include "TelemetryMetricsWorker.h"

struct ApplicationInfo
{
public:
    std::string ServiceName;
    std::string Environment;
    std::string Version;
    std::string RepositoryUrl;
    std::string CommitSha;

    std::shared_ptr<libdatadog::TelemetryMetricsWorker> Worker;
};
