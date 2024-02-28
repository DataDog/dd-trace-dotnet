// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IConfiguration.h"
#include "IProfilerTelemetry.h"


class ProfilerTelemetry : public IProfilerTelemetry
{
public:
    ProfilerTelemetry(IConfiguration* pConfiguration);
    ~ProfilerTelemetry() = default;

public:
    // from IProfilerTelemetry
    void ProcessStart(DeploymentMode deployment) override;
    void ProcessEnd(uint64_t duration) override;
    void SentProfile() override;
    void SkippedProfile(SkipProfileHeuristicType heuristic) override;

private:
    std::string GetDeploymentModeTag();

private:
    IConfiguration* m_pConfiguration;
    bool _isSsiDeployed = false;
};

