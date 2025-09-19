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
    _pConfiguration(pConfiguration),
    _deploymentMode{pConfiguration->GetDeploymentMode()},
    _longLivedThreshold{pConfiguration->GetSsiLongLivedThreshold()},
    _isStableConfigurationSet{false}
{
}

SsiManager::~SsiManager()
{
    _stopTimerPromise.set_value();
}

EnablementStatus SsiManager::GetCurrentEnabledStatus()
{
    return _pConfiguration->GetEnablementStatus();
}

void SsiManager::OnStableConfiguration()
{
    // Stable Configuration has been set by managed layer
    // check that it is not done more than once
    if (_isStableConfigurationSet)
    {
        Log::Warn("Stable configuration has already been set.");
        return;
    }

    _isStableConfigurationSet = true;

    auto enablementStatus = GetCurrentEnabledStatus();
    if (enablementStatus == EnablementStatus::ManuallyEnabled)
    {
        Log::Info("Profiler manually enabled");
        StartProfiling(_pSsiLifetime);
    }
    else
    if (enablementStatus == EnablementStatus::Auto)
    {
        Log::Info("Profiler enabled by SSI");

        // start services if heuristics have already been fulfilled
        if (_isLongLived &&_hasSpan)
        {
            StartProfiling(_pSsiLifetime);
        }
    }
    else
    {
        // should never happen, but if it does, log an error
        Log::Error("Unknown enablement status from Stable Configuration", static_cast<int>(enablementStatus));
    }
}

void SsiManager::OnShortLivedEnds()
{
    _isLongLived = true;


    auto enablementStatus = GetCurrentEnabledStatus();
    if (enablementStatus == EnablementStatus::Standby)
    {
        Log::Debug("OnShortLivedEnds() called, still waiting for Stable Configuration...");
        return;  // still waiting for enablement configuration from managed layer
    }

    if (enablementStatus == EnablementStatus::Auto)
    {
        Log::Debug("OnShortLivedEnds() called");

        if (_hasSpan)
        {
            Log::Debug("--> start profiling");
            StartProfiling(_pSsiLifetime);
        }
        else
        {
            Log::Debug("--> still no span");
        }
    }
}

void SsiManager::OnSpanCreated()
{
    // useful only for the first span
    if (_hasSpan)
    {
        return;
    }

    _hasSpan = true;

    auto enablementStatus = GetCurrentEnabledStatus();
    if (enablementStatus == EnablementStatus::Standby)
    {
        Log::Debug("OnSpanCreated() called, still waiting for Stable Configuration...");
        return; // still waiting for enablement configuration from managed layer
    }

    if (enablementStatus == EnablementStatus::Auto)
    {
        Log::Debug("OnSpanCreated() called");

        if (_isLongLived)
        {
            Log::Debug("--> start profiling");
            StartProfiling(_pSsiLifetime);
        }
        else
        {
            Log::Debug("--> still not long lived");
        }
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
//  or - the profiler is deployed via SSI and DD_INJECTION_ENABLED contains "profiler"
//
// WARNING: with Stable Configuration, the enablement status is set to Standby until the managed layer notifies the profiler
// that it is enabled or disabled. So this function will return false BEFORE the managed layer sets the enablement status.
bool SsiManager::IsProfilerEnabled()
{
    auto enablementStatus = GetCurrentEnabledStatus();
    if ((enablementStatus == EnablementStatus::Standby) ||
        (enablementStatus == EnablementStatus::ManuallyDisabled))
    {
        return false;
    }

    return enablementStatus == EnablementStatus::ManuallyEnabled ||
           enablementStatus == EnablementStatus::Auto;
}

// the profiler is activated (i.e. its providers services are started) either if:
//     - the profiler is enabled in the configuration (without Stable Configuration)
//  or - Stable Configuration enabled status is provided by managed layer + is enabled via SSI + runs for more than 30 seconds + has at least one span
bool SsiManager::IsProfilerStarted()
{
    auto enablementStatus = GetCurrentEnabledStatus();
    if (enablementStatus == EnablementStatus::Standby)
    {
        // still waiting for enablement configuration from managed layer
        return false;
    }

    return (enablementStatus == EnablementStatus::ManuallyEnabled) ||
           ((enablementStatus == EnablementStatus::Auto) && IsLongLived() && IsSpanCreated());
}

void SsiManager::ProcessStart()
{
    Log::Debug("ProcessStart(", to_string(_deploymentMode), ")");

    // Note: the deployment mode is not provided by Stable Configuration
    //       --> it is read from env var set during SSI installation
    if (_deploymentMode == DeploymentMode::SingleStepInstrumentation)
    {
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
