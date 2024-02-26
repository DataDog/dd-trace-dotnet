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
    void OnSpanCreated() override;
    bool IsSpanCreated() override;
    void ProcessStart() override;
    void ProcessEnd() override;
    void SentProfile() override;
    void SkippedProfile(SkipProfileHeuristicType heuristic) override;

private:
    std::string GetDeploymentState();

private:
    IConfiguration* m_pConfiguration;
    bool _hasSpan = false;
    bool _isSsiDeployed = false;
};

