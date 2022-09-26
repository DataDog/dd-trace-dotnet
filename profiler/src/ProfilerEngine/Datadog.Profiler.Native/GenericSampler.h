// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "AdaptiveSampler.h"
#include "IConfiguration.h"


// Base class for samplers that takes care of AdaptiveSampler logistics
class GenericSampler
{
private:
    static constexpr inline std::chrono::milliseconds SamplingWindow = std::chrono::milliseconds(500);

public:
    explicit GenericSampler(const IConfiguration* configuration);
    GenericSampler(std::chrono::milliseconds windowDuration, int32_t samplesPerWindow, int32_t lookback);
    virtual ~GenericSampler() = default;

    bool Sample();

protected:
    AdaptiveSampler _sampler;

protected:
    // Called to reset the state (if any) when starting a new window
    virtual void OnRollWindow();

private:
    int32_t SamplingWindowsPerRecording(const IConfiguration* configuration);
    int32_t SamplesPerWindow(const IConfiguration* configuration);
    void RollWindow();

};
