// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

enum class DeploymentMode
{
    Manual,
    SingleStepInstrumentation
};

inline std::string to_string(DeploymentMode mode)
{
    switch (mode)
    {
        case DeploymentMode::Manual:
            return "Manual";
        case DeploymentMode::SingleStepInstrumentation:
            return "Single Step Instrumentation";
    }

    return "Unknown Mode";
}