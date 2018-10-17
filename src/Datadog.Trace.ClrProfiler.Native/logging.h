#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include "util.h"

namespace trace {

void Log(const std::string& str);

template <typename... Args>
inline void Info(const Args... args) {
  Log("[info] " + ToString(args...));
}

template <typename... Args>
inline void Warn(const Args... args) {
  Log("[warn] " + ToString(args...));
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
