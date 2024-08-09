// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "DeploymentMode.h"
#include "EnablementStatus.h"
#include "ISsiManager.h"
#include "SkipProfileHeuristicType.h"

#include <future>
#include <memory>
#include <thread>

class IConfiguration;
class ISsiLifetime;

class SsiManager : public ISsiManager
{
public:
    // We need to pass another interface to notify when the profiler should start profiling
    // CorProfilerCallback is implementing this ISsiLifetime interface
    SsiManager(IConfiguration* pConfiguration, ISsiLifetime* pSsiLifetime);
    ~SsiManager();

public:
    // Inherited via ISsiManager
    void OnSpanCreated() override;
    bool IsSpanCreated() const override;
    bool IsLongLived() const override;
    bool IsProfilerEnabled() override;
    bool IsProfilerStarted() override;

    // send metrics
    void ProcessStart() override;
    void ProcessEnd() override;

    SkipProfileHeuristicType GetSkipProfileHeuristic() const override;

    DeploymentMode GetDeploymentMode() const override;

private:
    void OnShortLivedEnds();

private:
    ISsiLifetime* _pSsiLifetime;
    bool _hasSpan;
    bool _isLongLived;
    std::future<void> _longLivedTimerFuture;
    std::promise<void> _stopTimerPromise;
    DeploymentMode _deploymentMode;
    EnablementStatus _enablementStatus;
    std::chrono::milliseconds _longLivedThreshold;
};
