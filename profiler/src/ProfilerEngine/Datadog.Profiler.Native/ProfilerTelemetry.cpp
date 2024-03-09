// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfilerTelemetry.h"
#include "Log.h"

ProfilerTelemetry::ProfilerTelemetry(IConfiguration* pConfiguration)
   : _pConfiguration(pConfiguration)
{
}

std::string ProfilerTelemetry::GetDeploymentModeTag()
{
    return _isSsiDeployed ? "ssi" : "manual";
}

std::string ProfilerTelemetry::GetHeuristicTag(SkipProfileHeuristicType heuristics)
{
    std::string tags;
    if (heuristics == SkipProfileHeuristicType::AllTriggered)
    {
        return "AllTriggered";
    }

    if ((heuristics & SkipProfileHeuristicType::ShortLived) == SkipProfileHeuristicType::ShortLived)
    {
        tags = "ShortLived";
    }

    if ((heuristics & SkipProfileHeuristicType::NoSpan) == SkipProfileHeuristicType::NoSpan)
    {
        if (!tags.empty())
        {
            tags += " | ";
        }

        tags += "NoSpan";
    }

    return tags;
}

void ProfilerTelemetry::ProcessStart(DeploymentMode deployment)
{
    _isSsiDeployed = (deployment == DeploymentMode::SingleStepInstrumentation);

    Log::Debug("ProcessStart(", GetDeploymentModeTag(), ")");
}

void ProfilerTelemetry::ProcessEnd(uint64_t duration, uint64_t sentProfiles, SkipProfileHeuristicType heuristics)
{
    // provides:
    // - enablement choice (manual or SSI)
    auto enablementChoice = GetDeploymentModeTag();
    // - duration of the process
    // - number of profiles sent
    // - heuristics that were not triggered
    auto skippedHeuristics = GetHeuristicTag(heuristics);
    Log::Debug("ProcessEnd(", enablementChoice, ", ", duration, ", ", sentProfiles, ", ", skippedHeuristics, ")");
}
