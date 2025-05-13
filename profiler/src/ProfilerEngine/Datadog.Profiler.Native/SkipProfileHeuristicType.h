// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

enum SkipProfileHeuristicType
{
    AllTriggered = 0,
    ShortLived = 0x1,
    NoSpan = 0x2,
    // TODO: add new heuristics here
};

inline std::string to_string(SkipProfileHeuristicType heuristics)
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