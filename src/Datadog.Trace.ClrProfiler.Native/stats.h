#ifndef DD_CLR_PROFILER_STATS_H_
#define DD_CLR_PROFILER_STATS_H_

#include <chrono>

#include "util.h"

namespace trace
{

class SWStat
{
    std::atomic_ullong* _value;
    std::chrono::steady_clock::time_point _startTime;

public:
    SWStat(std::atomic_ullong* value)
    {
        _value = value;
        _startTime = std::chrono::steady_clock::now();
    }
    ~SWStat()
    {
        auto increment = (std::chrono::steady_clock::now() - _startTime).count();
        _value->fetch_add(increment);
    }
};

class Stats : public Singleton<Stats>
{
    friend class Singleton<Stats>;

private:
    std::atomic_ullong jitCachedFunctionSearchStarted = {0};
    std::atomic_ullong callTargetRequestRejit = {0};
    std::atomic_ullong callTargetRewriter = {0};
    std::atomic_ullong jitInlining = {0};
    std::atomic_ullong jitCompilationStarted = {0};
    std::atomic_ullong moduleUnloadStarted = {0};
    std::atomic_ullong moduleLoadFinished = {0};
    std::atomic_ullong assemblyLoadFinished = {0};
    std::atomic_ullong initialize = {0};

    //
    std::atomic_uint jitCachedFunctionSearchStartedCount = {0};
    std::atomic_uint callTargetRequestRejitCount = {0};
    std::atomic_uint callTargetRewriterCount = {0};
    std::atomic_uint jitInliningCount = {0};
    std::atomic_uint jitCompilationStartedCount = {0};
    std::atomic_uint moduleUnloadStartedCount = {0};
    std::atomic_uint moduleLoadFinishedCount = {0};
    std::atomic_uint assemblyLoadFinishedCount = {0};

public:
    Stats()
    {
        jitCachedFunctionSearchStarted = 0;
        callTargetRequestRejit = 0;
        jitInlining = 0;
        jitCompilationStarted = 0;
        moduleUnloadStarted = 0;
        moduleLoadFinished = 0;
        assemblyLoadFinished = 0;
        initialize = 0;

        jitCachedFunctionSearchStartedCount = 0;
        callTargetRequestRejitCount = 0;
        jitInliningCount = 0;
        jitCompilationStartedCount = 0;
        moduleUnloadStartedCount = 0;
        moduleLoadFinishedCount = 0;
        assemblyLoadFinishedCount = 0;
    }
    SWStat JITCachedFunctionSearchStartedMeasure()
    {
        jitCachedFunctionSearchStartedCount++;
        return SWStat(&jitCachedFunctionSearchStarted);
    }
    SWStat CallTargetRequestRejitMeasure()
    {
        callTargetRequestRejitCount++;
        return SWStat(&callTargetRequestRejit);
    }
    SWStat CallTargetRewriterCallbackMeasure()
    {
        callTargetRewriterCount++;
        return SWStat(&callTargetRewriter);
    }
    SWStat JITInliningMeasure()
    {
        jitInliningCount++;
        return SWStat(&jitInlining);
    }
    SWStat JITCompilationStartedMeasure()
    {
        jitCompilationStartedCount++;
        return SWStat(&jitCompilationStarted);
    }
    SWStat ModuleUnloadStartedMeasure()
    {
        moduleUnloadStartedCount++;
        return SWStat(&moduleUnloadStarted);
    }
    SWStat ModuleLoadFinishedMeasure()
    {
        moduleLoadFinishedCount++;
        return SWStat(&moduleLoadFinished);
    }
    SWStat AssemblyLoadFinishedMeasure()
    {
        assemblyLoadFinishedCount++;
        return SWStat(&assemblyLoadFinished);
    }
    SWStat InitializeMeasure()
    {
        return SWStat(&initialize);
    }
    std::string ToString()
    {
        std::stringstream ss;
        ss << "[Initialize=";
        ss << initialize.load() / 1000000 << "ms";
        ss << ", ModuleLoadFinished=";
        ss << moduleLoadFinished.load() / 1000000 << "ms"
           << "/" << moduleLoadFinishedCount.load();
        ss << ", CallTargetRequestRejit=";
        ss << callTargetRequestRejit.load() / 1000000 << "ms"
           << "/" << callTargetRequestRejitCount.load();
        ss << ", CallTargetRewriter=";
        ss << callTargetRewriter.load() / 1000000 << "ms"
           << "/" << callTargetRewriterCount.load();
        ss << ", AssemblyLoadFinished=";
        ss << assemblyLoadFinished.load() / 1000000 << "ms"
           << "/" << assemblyLoadFinishedCount.load();
        ss << ", ModuleUnloadStarted=";
        ss << moduleUnloadStarted.load() / 1000000 << "ms"
           << "/" << moduleUnloadStartedCount.load();
        ss << ", JitCompilationStarted=";
        ss << jitCompilationStarted.load() / 1000000 << "ms"
           << "/" << jitCompilationStartedCount.load();
        ss << ", JitInlining=";
        ss << jitInlining.load() / 1000000 << "ms"
           << "/" << jitInliningCount.load();
        ss << ", JitCacheFunctionSearchStarted=";
        ss << jitCachedFunctionSearchStarted.load() / 1000000 << "ms"
           << "/" << jitCachedFunctionSearchStartedCount.load();
        ss << "]";
        return ss.str();
    }
};

} // namespace trace

#endif // DD_CLR_PROFILER_STATS_H_