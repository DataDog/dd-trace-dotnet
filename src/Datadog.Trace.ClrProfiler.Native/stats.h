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
      _startTime = std::chrono::high_resolution_clock::now();
  }
  ~SWStat() { 
    *_value += std::chrono::high_resolution_clock::now() - _startTime;
  }
};

class Stats : public Singleton<Stats> {
  friend class Singleton<Stats>;

 private:
  std::chrono::nanoseconds jitInlining;
  std::chrono::nanoseconds jitCompilationStarted;
  std::chrono::nanoseconds moduleUnloadStarted;
  std::chrono::nanoseconds moduleLoadFinished;
  std::chrono::nanoseconds assemblyLoadFinished;

 public:
  Stats() { 
    jitInlining = std::chrono::nanoseconds(0);
    jitCompilationStarted = std::chrono::nanoseconds(0);
    moduleUnloadStarted = std::chrono::nanoseconds(0);
    moduleLoadFinished = std::chrono::nanoseconds(0);
    assemblyLoadFinished = std::chrono::nanoseconds(0);
  }
  SWStat JITInliningMeasure() { return SWStat(&jitInlining); }
  SWStat JITCompilationStartedMeasure() { return SWStat(&jitCompilationStarted); }
  SWStat ModuleUnloadStartedMeasure() { return SWStat(&moduleUnloadStarted); }
  SWStat ModuleLoadFinishedMeasure() { return SWStat(&moduleLoadFinished); }
  SWStat AssemblyLoadFinishedMeasure() { return SWStat(&assemblyLoadFinished); }
  std::string ToString() {
    std::stringstream ss;
    ss << "[ModuleLoadFinished=";
    ss << moduleLoadFinished.count() / 1000000 << "ms";
    ss << ", AssemblyLoadFinished=";
    ss << assemblyLoadFinished.count() / 1000000 << "ms";
    ss << ", ModuleUnloadStarted=";
    ss << moduleUnloadStarted.count() / 1000000 << "ms";
    ss << ", JitCompilationStarted=";
    ss << jitCompilationStarted.count() / 1000000 << "ms";
    ss << ", JitInlining=";
    ss << jitInlining.count() / 1000000 << "ms";
    ss << "]";
    return ss.str();
  }
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_STATS_H_