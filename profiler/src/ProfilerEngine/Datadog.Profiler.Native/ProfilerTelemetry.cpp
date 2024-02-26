// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfilerTelemetry.h"
#include "Log.h"

ProfilerTelemetry::ProfilerTelemetry(IConfiguration* pConfiguration)
   : m_pConfiguration(pConfiguration)
{
    _isSsiDeployed = pConfiguration->IsSsiDeployed();
}


void ProfilerTelemetry::OnSpanCreated()
{
    _hasSpan = true;
}

bool ProfilerTelemetry::IsSpanCreated()
{
    return _hasSpan;
}

std::string ProfilerTelemetry::GetDeploymentState()
{
    std::string state = "manual";
    if (_isSsiDeployed)
    {
        state = "ssi";
    }
    return state;
}

void ProfilerTelemetry::ProcessStart()
{
    Log::Debug("ProcessStart(", GetDeploymentState(), ")");
}

void ProfilerTelemetry::ProcessEnd()
{
    Log::Debug("ProcessEnd(", GetDeploymentState(), ")");
}

void ProfilerTelemetry::SentProfile()
{
    Log::Debug("SentProfile");
}

void ProfilerTelemetry::SkippedProfile(SkipProfileHeuristicType heuristic)
{
    if (heuristic == SkipProfileHeuristicType::ShortLived)
    {
        Log::Debug("SkippedProfile(ShortLived)");
    }
    else if (heuristic == SkipProfileHeuristicType::NoSpan)
    {
        Log::Debug("SkippedProfile(NoSpan)");
    }
    else
    {
        // detect invalid heuristic
        Log::Debug("SkippedProfile(Unknown)");
    }

}
