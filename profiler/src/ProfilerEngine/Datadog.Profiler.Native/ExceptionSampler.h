#pragma once
#include <unordered_set>

#include "AdaptiveSampler.h"
#include "IConfiguration.h"

class ExceptionSampler
{
public:
    explicit ExceptionSampler(const IConfiguration* configuration);
    ExceptionSampler(std::chrono::milliseconds windowDuration, int32_t samplesPerWindow, int32_t lookback);

    bool Sample(std::string exceptionType);

private:
    static constexpr inline std::chrono::milliseconds SamplingWindow = std::chrono::milliseconds(500);

    AdaptiveSampler _sampler;
    std::unordered_set<std::string> _knownExceptions;
    std::mutex _knownExceptionsMutex;

    void RollWindow();
    int SamplingWindowsPerRecording(const IConfiguration* configuration);
    int SamplesPerWindow(const IConfiguration* configuration);
};
