// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SsiManager.h"
#include "IConfiguration.h"
#include "IProfilerTelemetry.h"
#include "ISsiLifetime.h"
#include "OsSpecificApi.h"
#include "OpSysTools.h"


SsiManager::SsiManager(IConfiguration* pConfiguration, IProfilerTelemetry* pTelemetry, ISsiLifetime* pSsiLifetime)
    :
    _pConfiguration(pConfiguration),
    _pTelemetry(pTelemetry),
    _pSsiLifetime(pSsiLifetime),
    _timer([this] { OnShortLivedEnds(); }, std::chrono::milliseconds(_pConfiguration->SsiShortLivedThreshold()*1000))
{
    _isSsiDeployed = pConfiguration->IsSsiDeployed();
}

#ifdef DD_TEST
    void SsiManager::SetLifetimeDuration(int duration)
    {
        _lifetimeDuration = duration;
    }
#endif

void SsiManager::OnShortLivedEnds()
{
    _isLongLived = true;
    if (_hasSpan)
    {
        _pSsiLifetime->OnStartDelayedProfiling();
    }
}


void SsiManager::OnSpanCreated()
{
    _hasSpan = true;
    if (_isLongLived)
    {
        _pSsiLifetime->OnStartDelayedProfiling();
    }
}

bool SsiManager::IsSpanCreated()
{
    return _hasSpan;
}

bool SsiManager::IsLongLived()
{
#ifdef DD_TEST
    if (_lifetimeDuration > 0)
    {
        return true;
    }
    if (_lifetimeDuration == -1)
    {
        return false;
    }
#endif

    auto lifetime = OsSpecificApi::GetProcessLifetime();
    return lifetime > _pConfiguration->SsiShortLivedThreshold();
}

// the profiler is enabled if either:
//     - the profiler is enabled in the configuration
//  or - the profiler is deployed via SSI and DD_INJECTION_ENABLED contains "profiling"
bool SsiManager::IsProfilerEnabled()
{
    if (_pConfiguration->IsProfilerEnabled())
    {
        return true;
    }

    // in the future, users will be able to enable the profiler via SSI at agent installation time
    if (_pConfiguration->IsSsiEnabled())
    {
        return true;
    }

    return false;
}


// the profiler is activated either if:
//     - the profiler is enabled in the configuration
//  or - is enabled via SSI + runs for more than 30 seconds + has at least one span
bool SsiManager::IsProfilerActivated()
{
    if (_pConfiguration->IsProfilerEnabled())
    {
        return true;
    }

    // TODO: need to start a timer at the beginning of the process and use it if IsShortLived() is too expensive
    if (_isSsiDeployed && IsLongLived() && IsSpanCreated())
    {
        return true;
    }

    return false;
}

void SsiManager::ProcessStart()
{
    _pTelemetry->ProcessStart(_isSsiDeployed ? DeploymentMode::SingleStepInstrumentation : DeploymentMode::Manual);

    // start the lifetime timer to detect when the process is not more short lived
    _timer.Start();
}

void SsiManager::ProcessEnd()
{
    SkipProfileHeuristicType heuristics = SkipProfileHeuristicType::AllTriggered;

#ifdef DD_TEST
    if (_lifetimeDuration > 0)
    {
        if (!IsSpanCreated())
        {
            heuristics = (SkipProfileHeuristicType)(heuristics | SkipProfileHeuristicType::NoSpan);
        }
        _pTelemetry->ProcessEnd(_lifetimeDuration, 1, heuristics);
        return;
    }

    if (_lifetimeDuration == -1)
    {
        heuristics = (SkipProfileHeuristicType)(heuristics | SkipProfileHeuristicType::ShortLived);
        if (!IsSpanCreated())
        {
            heuristics = (SkipProfileHeuristicType)(heuristics | SkipProfileHeuristicType::NoSpan);
        }

        _pTelemetry->ProcessEnd(0, 1, heuristics);
        return;
    }
#endif

    // how to get the number of sent profiles?  process lifetime / exporter period (= Configuration::GetUploadInterval()) + 1
    // TODO: for IIS on Windows, it might be required to let the exporter count the number of sent profiles
    auto lifetime = OsSpecificApi::GetProcessLifetime();
    uint64_t sentProfiles = lifetime / _pConfiguration->GetUploadInterval().count() + 1;

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

