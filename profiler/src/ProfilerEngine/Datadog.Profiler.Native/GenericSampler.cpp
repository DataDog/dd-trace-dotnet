// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GenericSampler.h"
#include "Configuration.h"

GenericSampler::GenericSampler(const IConfiguration* configuration) :
    _sampler(SamplingWindow, SamplesPerWindow(configuration), SamplingWindowsPerRecording(configuration), 16, [this] { RollWindow(); })
{
}

GenericSampler::GenericSampler(std::chrono::milliseconds windowDuration, int32_t samplesPerWindow, int32_t lookback) :
    _sampler(windowDuration, samplesPerWindow, lookback, 16, [this] { RollWindow(); })
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

int32_t GenericSampler::SamplingWindowsPerRecording(const IConfiguration* configuration)
{
    const auto uploadIntervalMs = std::chrono::duration_cast<std::chrono::milliseconds>(configuration->GetUploadInterval());
    const auto samplingWindowMs = std::chrono::duration_cast<std::chrono::milliseconds>(SamplingWindow);
    return static_cast<int32_t>(std::min<int64_t>(uploadIntervalMs / samplingWindowMs, INT32_MAX));
}

int32_t GenericSampler::SamplesPerWindow(const IConfiguration* configuration)
{
    return configuration->ExceptionSampleLimit() / SamplingWindowsPerRecording(configuration);
}