#ifndef DD_CLR_PROFILER_STATS_H_
#define DD_CLR_PROFILER_STATS_H_

#include "util.h"
#include <chrono>

namespace trace {

class SWStat {
  std::chrono::nanoseconds *_value;
  std::chrono::steady_clock::time_point _startTime;

 public:
  SWStat(std::chrono::nanoseconds *value) {
      _value = value;
      _startTime = std::chrono::steady_clock::now();
  }
  ~SWStat() {
    *_value += std::chrono::steady_clock::now() - _startTime;
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
  unsigned int callTargetRequestRejitCount;
  unsigned int callTargetRewriterCount;
  unsigned int jitInliningCount;
  unsigned int jitCompilationStartedCount;
  unsigned int moduleUnloadStartedCount;
  unsigned int moduleLoadFinishedCount;
  unsigned int assemblyLoadFinishedCount;

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
    return SWStat(&callTargetRequestRejit);
  }
  SWStat CallTargetRewriterCallbackMeasure() {
    callTargetRewriterCount++;
    return SWStat(&callTargetRewriter);
  }
  SWStat JITInliningMeasure() {
    jitInliningCount++;
    return SWStat(&jitInlining);
  }
  SWStat JITCompilationStartedMeasure() {
    jitCompilationStartedCount++;
    return SWStat(&jitCompilationStarted);
  }
  SWStat ModuleUnloadStartedMeasure() {
    moduleUnloadStartedCount++;
    return SWStat(&moduleUnloadStarted);
  }
  SWStat ModuleLoadFinishedMeasure() {
    moduleLoadFinishedCount++;
    return SWStat(&moduleLoadFinished);
  }
  SWStat AssemblyLoadFinishedMeasure() {
    assemblyLoadFinishedCount++;
    return SWStat(&assemblyLoadFinished);
  }
  SWStat InitializeMeasure() {
    return SWStat(&initialize);
  }
  std::string ToString() {
    std::stringstream ss;
    ss << "[Initialize=";
    ss << initialize.count() / 1000000 << "ms";
    ss << ", ModuleLoadFinished=";
    ss << moduleLoadFinished.count() / 1000000 << "ms" << "/" << moduleLoadFinishedCount;
    ss << ", CallTargetRequestRejit=";
    ss << callTargetRequestRejit.count() / 1000000 << "ms" << "/" << callTargetRequestRejitCount;
    ss << ", CallTargetRewriter=";
    ss << callTargetRewriter.count() / 1000000 << "ms" << "/" << callTargetRewriterCount;
    ss << ", AssemblyLoadFinished=";
    ss << assemblyLoadFinished.count() / 1000000 << "ms" << "/" << assemblyLoadFinishedCount;
    ss << ", ModuleUnloadStarted=";
    ss << moduleUnloadStarted.count() / 1000000 << "ms" << "/" << moduleUnloadStartedCount;
    ss << ", JitCompilationStarted=";
    ss << jitCompilationStarted.count() / 1000000 << "ms" << "/" << jitCompilationStartedCount;
    ss << ", JitInlining=";
    ss << jitInlining.count() / 1000000 << "ms" << "/" << jitInliningCount;
    ss << "]";
    return ss.str();
  }
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_STATS_H_