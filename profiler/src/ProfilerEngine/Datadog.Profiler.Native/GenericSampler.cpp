// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GenericSampler.h"
#include "Configuration.h"


GenericSampler::GenericSampler(int32_t samplesLimit, int32_t uploadInterval) :
    _sampler(
        SamplingWindow,
        SamplesPerWindow(samplesLimit, SamplingWindowsPerRecording(uploadInterval, SamplingWindow.count())),
        SamplingWindowsPerRecording(uploadInterval, SamplingWindow.count()),
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

int32_t GenericSampler::SamplingWindowsPerRecording(int32_t intervalMs, int32_t samplingWindowMs)
{
    return intervalMs / samplingWindowMs;
}

int32_t GenericSampler::SamplesPerWindow(int32_t samplesLimit, int32_t samplingWindowsPerRecording)
{
    if (samplingWindowsPerRecording == 0)
    {
        return samplesLimit;
    }

    return samplesLimit / samplingWindowsPerRecording;
}