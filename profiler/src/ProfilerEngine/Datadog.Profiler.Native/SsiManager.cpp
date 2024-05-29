// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SsiManager.h"

#include "IConfiguration.h"
#include "IProfilerTelemetry.h"
#include "ISsiLifetime.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"

#include <functional>
#include <future>
#include <mutex>

static void StartProfiling(ISsiLifetime* pSsiLifetime)
{
    static std::once_flag spanCreated;
    std::call_once(spanCreated, [pSsiLifetime]() {
        pSsiLifetime->OnStartDelayedProfiling();
    });
}

SsiManager::SsiManager(IConfiguration* pConfiguration, IProfilerTelemetry* pTelemetry, ISsiLifetime* pSsiLifetime) :
    _pTelemetry(pTelemetry),
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
    if (_hasSpan && _deploymentMode == DeploymentMode::SingleStepInstrumentation)
    {
        StartProfiling(_pSsiLifetime);
    }
}

void SsiManager::OnSpanCreated()
{
    _hasSpan = true;
    if (_isLongLived && _deploymentMode == DeploymentMode::SingleStepInstrumentation)
    {
        StartProfiling(_pSsiLifetime);
    }
}

bool SsiManager::IsSpanCreated()
{
    return _hasSpan;
}

bool SsiManager::IsLongLived()
{
    auto lifetime = OsSpecificApi::GetProcessLifetime();
    return lifetime > _longLivedThreshold.count();
}

// the profiler is enabled if either:
//     - the profiler is enabled in the configuration
//  or - the profiler is deployed via SSI and DD_INJECTION_ENABLED contains "profiling"
bool SsiManager::IsProfilerEnabled()
{
    auto enablementStatus = _enablementStatus;
    return enablementStatus == EnablementStatus::ManuallyEnabled ||
           // in the future, users will be able to enable the profiler via SSI at agent installation time
           enablementStatus == EnablementStatus::SsiEnabled;
}

// the profiler is activated either if:
//     - the profiler is enabled in the configuration
//  or - is enabled via SSI + runs for more than 30 seconds + has at least one span
bool SsiManager::IsProfilerActivated()
{
    if (_enablementStatus == EnablementStatus::ManuallyEnabled)
    {
        return true;
    }

    /// should we return enablementstatus::SsiEnabled ?
    // TODO: need to start a timer at the beginning of the process and use it if IsShortLived() is too expensive
    if (_deploymentMode == DeploymentMode::SingleStepInstrumentation && IsLongLived() && IsSpanCreated())
    {
        return true;
    }

    return false;
}

void SsiManager::ProcessStart()
{
    _pTelemetry->ProcessStart(_deploymentMode);

    if (_deploymentMode == DeploymentMode::SingleStepInstrumentation && !IsProfilerEnabled())
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
    SkipProfileHeuristicType heuristics = SkipProfileHeuristicType::AllTriggered;

    // how to get the number of sent profiles?  process lifetime / exporter period (= Configuration::GetUploadInterval()) + 1
    // TODO: for IIS on Windows, it might be required to let the exporter count the number of sent profiles
    auto lifetime = OsSpecificApi::GetProcessLifetime();
    uint64_t sentProfiles = lifetime / std::chrono::duration_cast<std::chrono::seconds>(_longLivedThreshold).count() + 1;

    // compute the non triggered heuristics
    heuristics = (SkipProfileHeuristicType)(heuristics | GetSkipProfileHeuristic());

    _pTelemetry->ProcessEnd((uint64_t)lifetime, sentProfiles, heuristics);
}

SkipProfileHeuristicType SsiManager::GetSkipProfileHeuristic()
{
    SkipProfileHeuristicType heuristics = SkipProfileHeuristicType::AllTriggered;

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
