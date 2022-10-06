// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>

#include "AdaptiveSampler.h"
#include "IConfiguration.h"


using namespace std::literals;


// Base class for samplers that takes care of AdaptiveSampler logistics
class GenericSampler
{
private:
    static constexpr inline std::chrono::milliseconds SamplingWindow = 500ms;

public:
    GenericSampler(int32_t samplesLimit, std::chrono::seconds uploadInterval);
    virtual ~GenericSampler() = default;

    bool Sample();

protected:
    AdaptiveSampler _sampler;

protected:
    // Called to reset the state (if any) when starting a new window
    virtual void OnRollWindow();

private:
    static int32_t SamplingWindowsPerRecording(std::chrono::seconds intervalSec, std::chrono::milliseconds samplingWindowMs);
    static int32_t SamplesPerWindow(int32_t samplesLimit, int32_t samplingWindowsPerRecording);
    void RollWindow();

};
