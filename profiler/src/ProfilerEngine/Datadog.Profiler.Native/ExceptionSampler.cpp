#include "ExceptionSampler.h"

ExceptionSampler::ExceptionSampler(const IConfiguration* configuration) :
    _sampler(SamplingWindow, SamplesPerWindow(configuration), SamplingWindowsPerRecording(configuration), 16, [this] { RollWindow(); })
{
}

ExceptionSampler::ExceptionSampler(std::chrono::milliseconds windowDuration, int32_t samplesPerWindow, int32_t lookback) :
    _sampler(windowDuration, samplesPerWindow, lookback, 16, [this] { RollWindow(); })
{
}

bool ExceptionSampler::Sample(const std::string exceptionType)
{
    {
        std::unique_lock lock(_knownExceptionsMutex);

        if (_knownExceptions.find(exceptionType) == _knownExceptions.end())
        {
            // This is the first time we see this exception in this time window,
            // force the sampling decision
            _knownExceptions.insert(exceptionType);

            return _sampler.Keep();
        }
    }

    // We've already seen this exception, let the sampler decide
    return _sampler.Sample();
}

void ExceptionSampler::RollWindow()
{
    std::unique_lock lock(_knownExceptionsMutex);
    _knownExceptions.clear();
}

int32_t ExceptionSampler::SamplingWindowsPerRecording(const IConfiguration* configuration)
{
    const auto uploadIntervalMs = std::chrono::duration_cast<std::chrono::milliseconds>(configuration->GetUploadInterval());
    const auto samplingWindowMs = std::chrono::duration_cast<std::chrono::milliseconds>(SamplingWindow);
    return static_cast<int32_t>(std::min<int64_t>(uploadIntervalMs / samplingWindowMs, INT32_MAX));
}

int32_t ExceptionSampler::SamplesPerWindow(const IConfiguration* configuration)
{
    return configuration->ExceptionSampleLimit() / SamplingWindowsPerRecording(configuration);
}
