// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SsiManager.h"

#include "IConfiguration.h"
#include "ISsiLifetime.h"
#include "Log.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"

#include <functional>
#include <future>
#include <mutex>

static void StartProfiling(ISsiLifetime* pSsiLifetime)
{
    static std::once_flag heuristicsAreTriggered;
    std::call_once(heuristicsAreTriggered, [pSsiLifetime]() {
        Log::Info("Profiling delayed start");
        pSsiLifetime->OnStartDelayedProfiling();
    });
}

SsiManager::SsiManager(IConfiguration* pConfiguration, ISsiLifetime* pSsiLifetime) :
    _pSsiLifetime(pSsiLifetime),
    _hasSpan{false},
    _isLongLived{false},
    _deploymentMode{pConfiguration->GetDeploymentMode()},
    _enablementStatus{pConfiguration->GetEnablementStatus()},
    _longLivedThreshold{pConfiguration->GetSsiLongLivedThreshold()}
{
}

SsiManager::~SsiManager()
{
    _stopTimerPromise.set_value();
}

void SsiManager::OnShortLivedEnds()
{
    _isLongLived = true;
    if (_hasSpan && ((_enablementStatus == EnablementStatus::SsiEnabled) || (_enablementStatus == EnablementStatus::Auto)))
    {
        StartProfiling(_pSsiLifetime);
    }
}

void SsiManager::OnSpanCreated()
{
    _hasSpan = true;
    if (_isLongLived && ((_enablementStatus == EnablementStatus::SsiEnabled) || (_enablementStatus == EnablementStatus::Auto)))
    {
        StartProfiling(_pSsiLifetime);
    }
}

bool SsiManager::IsSpanCreated() const
{
    return _hasSpan;
}

bool SsiManager::IsLongLived() const
{
    return _isLongLived;
}

// the profiler is enabled if either:
//     - the profiler is enabled in the configuration (including "auto")
//  or - the profiler is deployed via SSI and DD_INJECTION_ENABLED contains "profiling"
bool SsiManager::IsProfilerEnabled()
{
    return _enablementStatus == EnablementStatus::ManuallyEnabled ||
           _enablementStatus == EnablementStatus::Auto ||
           // in the future, users will be able to enable the profiler via SSI at agent installation time
           _enablementStatus == EnablementStatus::SsiEnabled;
}

// the profiler is activated either if:
//     - the profiler is enabled in the configuration
//  or - is enabled via SSI + runs for more than 30 seconds + has at least one span
bool SsiManager::IsProfilerStarted()
{
    return _enablementStatus == EnablementStatus::ManuallyEnabled ||
           (((_enablementStatus == EnablementStatus::Auto) || (_enablementStatus == EnablementStatus::SsiEnabled)) && IsLongLived() && IsSpanCreated());
}

void SsiManager::ProcessStart()
{
    Log::Debug("ProcessStart(", to_string(_deploymentMode), ")");

    // TODO the doc again to know when we need the timer.
    // currently it's disabled in ssi deployed AND not manually enabled nor ssi enabled
    // I guess we still have to start the timer when ssi enabled
    if (_deploymentMode == DeploymentMode::SingleStepInstrumentation)
    {
        // This timer *must* be created only AND only if it's a SSI deployment
        // we have to check if this is what we want. In CorProfilerCallback.cpp l.1239, we start the service
        // if the profiler is enabled (SII or not SSI).
        // For the moment we just enable the timer only in pure SSI
        _longLivedTimerFuture = std::async(
            std::launch::async, [this](std::future<void> stopRequest) {
                auto status = stopRequest.wait_for(_longLivedThreshold);
                if (status == std::future_status::timeout)
                {
                    OnShortLivedEnds();
                }
            },
            _stopTimerPromise.get_future());
    }
}

void SsiManager::ProcessEnd()
{
    Log::Debug("ProcessEnd(", to_string(_deploymentMode), ", ", to_string(GetSkipProfileHeuristic()), ")");
}

SkipProfileHeuristicType SsiManager::GetSkipProfileHeuristic() const
{
    auto heuristics = SkipProfileHeuristicType::AllTriggered;

    if (!IsLongLived())
    {
        heuristics = (SkipProfileHeuristicType)(heuristics | SkipProfileHeuristicType::ShortLived);
    }
    if (!IsSpanCreated())
    {
        heuristics = (SkipProfileHeuristicType)(heuristics | SkipProfileHeuristicType::NoSpan);
    }

    return heuristics;
}

DeploymentMode SsiManager::GetDeploymentMode() const
{
    return _deploymentMode;
}
