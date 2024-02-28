// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfilerTelemetry.h"
#include "Log.h"

ProfilerTelemetry::ProfilerTelemetry(IConfiguration* pConfiguration)
   : m_pConfiguration(pConfiguration)
{
}

std::string ProfilerTelemetry::GetDeploymentModeTag()
{
    return _isSsiDeployed ? "ssi" : "manual";
}

void ProfilerTelemetry::ProcessStart(DeploymentMode deployment)
{
    _isSsiDeployed = (deployment == DeploymentMode::SingleStepInstrumentation);

    Log::Debug("ProcessStart(", GetDeploymentModeTag(), ")");
}

void ProfilerTelemetry::ProcessEnd(uint64_t duration)
{
    Log::Debug("ProcessEnd(", GetDeploymentModeTag(), ", ", duration, ")");
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
