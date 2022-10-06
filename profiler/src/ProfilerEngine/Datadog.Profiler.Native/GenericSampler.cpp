// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <chrono>

#include "GenericSampler.h"
#include "Configuration.h"


GenericSampler::GenericSampler(int32_t samplesLimit, std::chrono::seconds uploadInterval) :
    _sampler(
        SamplingWindow,
        SamplesPerWindow(samplesLimit, SamplingWindowsPerRecording(uploadInterval, SamplingWindow)),
        SamplingWindowsPerRecording(uploadInterval, SamplingWindow),
        16, [this] { RollWindow(); }
        )
{
}

bool GenericSampler::Sample()
{
    return _sampler.Sample();
}

void GenericSampler::RollWindow()
{
    OnRollWindow();
}

void GenericSampler::OnRollWindow()
{
}

int32_t GenericSampler::SamplingWindowsPerRecording(std::chrono::seconds intervalSec, std::chrono::milliseconds samplingWindowMs)
{
    const auto uploadIntervalMs = std::chrono::duration_cast<std::chrono::milliseconds>(intervalSec);

    return static_cast<int32_t>(uploadIntervalMs / samplingWindowMs);
}

int32_t GenericSampler::SamplesPerWindow(int32_t samplesLimit, int32_t samplingWindowsPerRecording)
{
    if (samplingWindowsPerRecording == 0)
    {
        return samplesLimit;
    }

    return samplesLimit / samplingWindowsPerRecording;
}