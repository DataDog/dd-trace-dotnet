#pragma once

#include <atomic>
#include <chrono>
#include <random>
#include <mutex>

#include "Timer.h"

class AdaptiveSampler
{
    class Counts
    {
    public:
        void AddTest();
        bool AddSample(int64_t limit);
        void AddSample();
        void Reset();
        int64_t SampleCount();
        int64_t TestCount();

    private:
        std::atomic<int64_t> _testCount;
        std::atomic<int64_t> _sampleCount;
    };

    class State
    {
    public:
        int64_t TestCount;
        int64_t SampleCount;
        int64_t Budget;
        double Probability;
        double TotalAverage;
    };

public:
    AdaptiveSampler(std::chrono::milliseconds windowDuration, int32_t samplesPerWindow, int32_t averageLookback, int32_t budgetLookback, std::function<void()> rollWindowCallback);

    bool Sample();
    bool Keep();
    bool Drop();

    void RollWindow();

    // For tests
    State GetInternalState();

    void Stop();

private:
    double _emaAlpha;
    int32_t _samplesPerWindow;

    std::atomic<Counts*> _countsRef;

    volatile double _probability = 1;
    volatile int64_t _samplesBudget;

    double _totalCountRunningAverage;
    double _avgSamples;

    int32_t _budgetLookback;
    double _budgetAlpha;

    int32_t _countsSlotIndex = 0;
    Counts _countsSlots[2];

    Timer _timer;
    std::mutex _callbackMutex;
    std::function<void()> _rollWindowCallback;

    std::mutex _rngMutex;
    std::uniform_real_distribution<> _distribution;
    std::mt19937 _rng;

    static double ComputeIntervalAlpha(int32_t lookback);

    double NextDouble();
    int64_t CalculateBudgetEma(int64_t sampledCount);
};
