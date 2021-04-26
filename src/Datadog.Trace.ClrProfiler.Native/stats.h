#ifndef DD_CLR_PROFILER_STATS_H_
#define DD_CLR_PROFILER_STATS_H_

#include <chrono>

#include "util.h"

namespace trace {

class SWStat {
  std::chrono::nanoseconds* _value;
  std::mutex* _mutex;
  std::chrono::steady_clock::time_point _startTime;

 public:
  SWStat(std::chrono::nanoseconds* value, std::mutex* mutex) {
    _value = value;
    _mutex = mutex;
    _startTime = std::chrono::steady_clock::now();
  }
  ~SWStat() {
    auto increment = std::chrono::steady_clock::now() - _startTime;
    {
      std::lock_guard<std::mutex> guard(*_mutex);
      *_value += increment;
    }
  }
};

class Stats : public Singleton<Stats> {
  friend class Singleton<Stats>;

 private:
  std::chrono::nanoseconds callTargetRequestRejit;
  std::chrono::nanoseconds callTargetRewriter;
  std::chrono::nanoseconds jitInlining;
  std::chrono::nanoseconds jitCompilationStarted;
  std::chrono::nanoseconds moduleUnloadStarted;
  std::chrono::nanoseconds moduleLoadFinished;
  std::chrono::nanoseconds assemblyLoadFinished;
  std::chrono::nanoseconds initialize;

  //
  std::atomic_uint callTargetRequestRejitCount = {0};
  std::atomic_uint callTargetRewriterCount = {0};
  std::atomic_uint jitInliningCount = {0};
  std::atomic_uint jitCompilationStartedCount = {0};
  std::atomic_uint moduleUnloadStartedCount = {0};
  std::atomic_uint moduleLoadFinishedCount = {0};
  std::atomic_uint assemblyLoadFinishedCount = {0};

  //
  std::mutex callTargetRequestRejitMutex;
  std::mutex callTargetRewriterMutex;
  std::mutex jitInliningMutex;
  std::mutex jitCompilationStartedMutex;
  std::mutex moduleUnloadStartedMutex;
  std::mutex moduleLoadFinishedMutex;
  std::mutex assemblyLoadFinishedMutex;
  std::mutex initializeMutex;

 public:
  Stats() {
    callTargetRequestRejit = std::chrono::nanoseconds(0);
    jitInlining = std::chrono::nanoseconds(0);
    jitCompilationStarted = std::chrono::nanoseconds(0);
    moduleUnloadStarted = std::chrono::nanoseconds(0);
    moduleLoadFinished = std::chrono::nanoseconds(0);
    assemblyLoadFinished = std::chrono::nanoseconds(0);
    initialize = std::chrono::nanoseconds(0);

    callTargetRequestRejitCount = 0;
    jitInliningCount = 0;
    jitCompilationStartedCount = 0;
    moduleUnloadStartedCount = 0;
    moduleLoadFinishedCount = 0;
    assemblyLoadFinishedCount = 0;
  }
  SWStat CallTargetRequestRejitMeasure() {
    callTargetRequestRejitCount++;

    return SWStat(&callTargetRequestRejit, &callTargetRequestRejitMutex);
  }
  SWStat CallTargetRewriterCallbackMeasure() {
    callTargetRewriterCount++;
    return SWStat(&callTargetRewriter, &callTargetRewriterMutex);
  }
  SWStat JITInliningMeasure() {
    jitInliningCount++;
    return SWStat(&jitInlining, &jitInliningMutex);
  }
  SWStat JITCompilationStartedMeasure() {
    jitCompilationStartedCount++;
    return SWStat(&jitCompilationStarted, &jitCompilationStartedMutex);
  }
  SWStat ModuleUnloadStartedMeasure() {
    moduleUnloadStartedCount++;
    return SWStat(&moduleUnloadStarted, &moduleUnloadStartedMutex);
  }
  SWStat ModuleLoadFinishedMeasure() {
    moduleLoadFinishedCount++;
    return SWStat(&moduleLoadFinished, &moduleLoadFinishedMutex);
  }
  SWStat AssemblyLoadFinishedMeasure() {
    assemblyLoadFinishedCount++;
    return SWStat(&assemblyLoadFinished, &assemblyLoadFinishedMutex);
  }
  SWStat InitializeMeasure() {
      return SWStat(&initialize, &initializeMutex);
  }
  std::string ToString() {
    std::stringstream ss;
    ss << "[Initialize=";
    ss << initialize.count() / 1000000 << "ms";
    ss << ", ModuleLoadFinished=";
    ss << moduleLoadFinished.count() / 1000000 << "ms"
       << "/" << moduleLoadFinishedCount;
    ss << ", CallTargetRequestRejit=";
    ss << callTargetRequestRejit.count() / 1000000 << "ms"
       << "/" << callTargetRequestRejitCount;
    ss << ", CallTargetRewriter=";
    ss << callTargetRewriter.count() / 1000000 << "ms"
       << "/" << callTargetRewriterCount;
    ss << ", AssemblyLoadFinished=";
    ss << assemblyLoadFinished.count() / 1000000 << "ms"
       << "/" << assemblyLoadFinishedCount;
    ss << ", ModuleUnloadStarted=";
    ss << moduleUnloadStarted.count() / 1000000 << "ms"
       << "/" << moduleUnloadStartedCount;
    ss << ", JitCompilationStarted=";
    ss << jitCompilationStarted.count() / 1000000 << "ms"
       << "/" << jitCompilationStartedCount;
    ss << ", JitInlining=";
    ss << jitInlining.count() / 1000000 << "ms"
       << "/" << jitInliningCount;
    ss << "]";
    return ss.str();
  }
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_STATS_H_