// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IConfiguration.h"
#include "IProfilerTelemetry.h"
#include "IExporter.h"

class ProfilerTelemetry : public IProfilerTelemetry
{
public:
    ProfilerTelemetry(IConfiguration* pConfiguration);
    ~ProfilerTelemetry() = default;

public:
    // from IProfilerTelemetry
    void ProcessStart(DeploymentMode deployment) override;
    void ProcessEnd(uint64_t duration, uint64_t sentProfiles, SkipProfileHeuristicType heuristics) override;
    void SetExporter(IExporter* pExporter) override;

private:
    std::string GetDeploymentModeTag();
    std::string GetHeuristicTag(SkipProfileHeuristicType heuristics);
    void SendMetrics(uint64_t duration, uint64_t sentProfiles, SkipProfileHeuristicType heuristics);

private:
    IConfiguration* _pConfiguration;
    bool _isSsiDeployed = false;
    IExporter* _pExporter;
};

