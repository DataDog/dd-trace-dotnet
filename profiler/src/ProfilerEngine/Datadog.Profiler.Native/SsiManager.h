// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ISsiManager.h"
#include "IProfilerTelemetry.h"
#include "Timer.h"

#include <memory>
#include <thread>

class IConfiguration;
class ISsiLifetime;

class SsiManager : public ISsiManager
{
public:
    // We need to pass another interface to notify when the profiler should start profiling
    // CorProfilerCallback is implementing this ISsiLifetime interface
    SsiManager(IConfiguration* pConfiguration, IProfilerTelemetry* pTelemetry, ISsiLifetime* pSsiLifetime);
    ~SsiManager() = default;

#ifdef DD_TEST
public:
    void SetLifetimeDuration(int duration)
    {
        _lifetimeDuration = duration;
    }
#endif

public:
    // Inherited via ISsiManager
    void OnSpanCreated() override;
    bool IsSpanCreated() override;
    bool IsLongLived() override;
    bool IsProfilerEnabled() override;
    bool IsProfilerActivated() override;

    // send metrics
    void ProcessStart() override;
    void ProcessEnd() override;

    SkipProfileHeuristicType GetSkipProfileHeuristic() override;

private:
    void OnShortLivedEnds();

private:
    IConfiguration* _pConfiguration;
    IProfilerTelemetry* _pTelemetry;
    ISsiLifetime* _pSsiLifetime;
    bool _hasSpan = false;
    bool _isLongLived = false;
    bool _isSsiDeployed = false;
    std::unique_ptr<Timer> _timer;

#ifdef DD_TEST
private:
    //  -1 means short lived
    //   0 means normal lifetime computing
    // > 0 means long lived
    int _lifetimeDuration = 0;
#endif
};

