#ifndef DD_CLR_PROFILER_STATS_H_
#define DD_CLR_PROFILER_STATS_H_

#include <chrono>

#include "../../../shared/src/native-src/util.h"

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
        Refresh();
    }
    void Refresh()
    {
        auto now = std::chrono::steady_clock::now();
        auto increment = (now - _startTime).count();
        _startTime = now;
        _value->fetch_add(increment);
    }
};

class Stats : public shared::Singleton<Stats>
{
    friend class shared::Singleton<Stats>;

private:
    std::atomic_ullong totalTime = {0};
    std::unique_ptr<SWStat> totalTimeCounter = nullptr;

    std::atomic_ullong initializeProfiler = {0};
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
    std::atomic_uint initializeProfilerCount = {0};
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
        totalTime = 0;
        totalTimeCounter = std::make_unique<SWStat>(&totalTime);

        initializeProfiler = 0;
        jitCachedFunctionSearchStarted = 0;
        callTargetRequestRejit = 0;
        jitInlining = 0;
        jitCompilationStarted = 0;
        moduleUnloadStarted = 0;
        moduleLoadFinished = 0;
        assemblyLoadFinished = 0;
        initialize = 0;

        initializeProfilerCount = 0;
        jitCachedFunctionSearchStartedCount = 0;
        callTargetRequestRejitCount = 0;
        jitInliningCount = 0;
        jitCompilationStartedCount = 0;
        moduleUnloadStartedCount = 0;
        moduleLoadFinishedCount = 0;
        assemblyLoadFinishedCount = 0;
    }
    SWStat InitializeProfilerMeasure()
    {
        initializeProfilerCount++;
        return SWStat(&initializeProfiler);
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
        const auto ns_initialize = initialize.load();
        const auto ns_moduleLoadFinished = moduleLoadFinished.load();
        const auto ns_callTargetRequestRejit = callTargetRequestRejit.load();
        const auto ns_callTargetRewriter = callTargetRewriter.load();
        const auto ns_assemblyLoadFinished = assemblyLoadFinished.load();
        const auto ns_moduleUnloadStarted = moduleUnloadStarted.load();
        const auto ns_jitCompilationStarted = jitCompilationStarted.load();
        const auto ns_jitInlining = jitInlining.load();
        const auto ns_jitCachedFunctionSearchStarted = jitCachedFunctionSearchStarted.load();
        const auto ns_initializeProfiler = initializeProfiler.load();

        const auto count_moduleLoadFinishedCount = moduleLoadFinishedCount.load();
        const auto count_callTargetRequestRejitCount = callTargetRequestRejitCount.load();
        const auto count_callTargetRewriterCount = callTargetRewriterCount.load();
        const auto count_assemblyLoadFinishedCount = assemblyLoadFinishedCount.load();
        const auto count_moduleUnloadStartedCount = moduleUnloadStartedCount.load();
        const auto count_jitCompilationStartedCount = jitCompilationStartedCount.load();
        const auto count_jitInliningCount = jitInliningCount.load();
        const auto count_jitCachedFunctionSearchStartedCount = jitCachedFunctionSearchStartedCount.load();
        const auto count_initializeProfilerCount = initializeProfilerCount.load();

        const auto ns_total = ns_initialize + ns_moduleLoadFinished + ns_callTargetRequestRejit +
                              ns_callTargetRewriter + ns_assemblyLoadFinished + ns_moduleUnloadStarted +
                              ns_jitCompilationStarted + ns_jitInlining + ns_jitCachedFunctionSearchStarted +
                              ns_initializeProfiler;

        totalTimeCounter->Refresh();
        const auto ns_fromBeginToEndTotal = totalTime.load();

        std::stringstream ss;
        ss << "Total time: ";
        ss << ns_fromBeginToEndTotal / 1000000 << "ms";
        ss << " | Total time in Callbacks: ";
        ss << ns_total / 1000000 << "ms ";
        ss << "[Initialize=";
        ss << ns_initialize / 1000000 << "ms";
        ss << ", ModuleLoadFinished=";
        ss << ns_moduleLoadFinished / 1000000 << "ms"
           << "/" << count_moduleLoadFinishedCount;
        ss << ", CallTargetRequestRejit=";
        ss << ns_callTargetRequestRejit / 1000000 << "ms"
           << "/" << count_callTargetRequestRejitCount;
        ss << ", CallTargetRewriter=";
        ss << ns_callTargetRewriter / 1000000 << "ms"
           << "/" << count_callTargetRewriterCount;
        ss << ", AssemblyLoadFinished=";
        ss << ns_assemblyLoadFinished / 1000000 << "ms"
           << "/" << count_assemblyLoadFinishedCount;
        ss << ", ModuleUnloadStarted=";
        ss << ns_moduleUnloadStarted / 1000000 << "ms"
           << "/" << count_moduleUnloadStartedCount;
        ss << ", JitCompilationStarted=";
        ss << ns_jitCompilationStarted / 1000000 << "ms"
           << "/" << count_jitCompilationStartedCount;
        ss << ", JitInlining=";
        ss << ns_jitInlining / 1000000 << "ms"
           << "/" << count_jitInliningCount;
        ss << ", JitCacheFunctionSearchStarted=";
        ss << ns_jitCachedFunctionSearchStarted / 1000000 << "ms"
           << "/" << count_jitCachedFunctionSearchStartedCount;
        ss << ", InitializeProfiler=";
        ss << ns_initializeProfiler / 1000000 << "ms"
           << "/" << count_initializeProfilerCount;
        ss << "]";
        return ss.str();
    }
};

} // namespace trace

#endif // DD_CLR_PROFILER_STATS_H_