#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include "string.h"  // NOLINT

namespace trace {

extern bool debug_logging_enabled;

void Log(const std::string &str);

template <typename Arg>
std::string LogToString(Arg const &arg) {
  return ToString(arg);
}

template <typename... Args>
std::string LogToString(Args const &... args) {
  std::ostringstream oss;
  int a[] = {0, ((void)(oss << LogToString(args)), 0)...};
  return oss.str();
}

template <typename... Args>
void Debug(const Args... args) {
  if (debug_logging_enabled) {
    Log("[debug] " + LogToString(args...));
  }
}

template <typename... Args>
void Info(const Args... args) {
  Log("[info] " + LogToString(args...));
}

template <typename... Args>
void Warn(const Args... args) {
  Log("[warn] " + LogToString(args...));
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
