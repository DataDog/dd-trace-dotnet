#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include "string.h"

namespace trace {

void Log(const std::string &str);

template <typename Arg>
inline std::string LogToString(Arg const &arg) {
  return ToString(arg);
}

template <typename... Args>
inline std::string LogToString(Args const &... args) {
  std::ostringstream oss;
  int a[] = {0, ((void)(oss << LogToString(args) << " "), 0)...};
  return oss.str();
}

template <typename... Args>
inline void Info(const Args... args) {
  Log("[info] " + LogToString(args...));
}

template <typename... Args>
inline void Warn(const Args... args) {
  Log("[warn] " + LogToString(args...));
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
