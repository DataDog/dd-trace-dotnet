// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SsiManager.h"
#include "IConfiguration.h"
#include "IProfilerTelemetry.h"
#include "OsSpecificApi.h"

SsiManager::SsiManager(IConfiguration* pConfiguration, IProfilerTelemetry* pTelemetry)
    :
    _pConfiguration(pConfiguration),
    _pTelemetry(pTelemetry)
{
    _isSsiDeployed = pConfiguration->IsSsiDeployed();
}

#ifdef DD_TEST
    void SsiManager::SetLifetimeDuration(int duration)
    {
        _lifetimeDuration = duration;
    }
#endif


void SsiManager::OnSpanCreated()
{
    _hasSpan = true;
}

bool SsiManager::IsSpanCreated()
{
    return _hasSpan;
}

bool SsiManager::IsShortLived()
{
#ifdef DD_TEST
    if (_lifetimeDuration > 0)
    {
        return false;
    }
    if (_lifetimeDuration == -1)
    {
        return true;
    }
#endif

    auto lifetime = OsSpecificApi::GetProcessLifetime();
    if (lifetime < 30) // TODO add a configuration for this value
    {
        return true;
    }

    return false;
}

// the profiler is activated either if:
//     - the profiler is enabled in the configuration
//  or - is deployed via SSI + runs for more than 30 seconds + has at least one span
//
// In the future, we might also be activated by SSI based on user's choice
bool SsiManager::IsProfilerActivated()
{
    if (_pConfiguration->IsProfilerEnabled())
    {
        return true;
    }

    // TODO: need to start a timer at the beginning of the process and use it if IsShortLived() is too expensive
    if (_isSsiDeployed && !IsShortLived() && IsSpanCreated())
    {
        return true;
    }

    return false;
}

void SsiManager::ProcessStart()
{
    _pTelemetry->ProcessStart(_isSsiDeployed ? DeploymentMode::SingleStepInstrumentation : DeploymentMode::Manual);
}

void SsiManager::ProcessEnd()
{
#ifdef DD_TEST
    if (_lifetimeDuration > 0)
    {
        _pTelemetry->ProcessEnd(_lifetimeDuration);
        return;
    }
#endif

    auto lifetime = OsSpecificApi::GetProcessLifetime();
    _pTelemetry->ProcessEnd((uint64_t)lifetime);
}

bool SsiManager::ShouldSendProfile(const std::string& env, const std::string& serviceName, const std::string_view& runtimeId)
{
    if (!IsSpanCreated())
    {
        _pTelemetry->SkippedProfile(SkipProfileHeuristicType::NoSpan);
        return false;
    }

    if (IsShortLived())
    {
        _pTelemetry->SkippedProfile(SkipProfileHeuristicType::ShortLived);
        return false;
    }

    _pTelemetry->SentProfile();
    return true;
}

