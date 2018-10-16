#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include <fmt/core.h>
#include <array>
#include <iostream>
#include <sstream>
#include <string>

namespace trace {

std::string toString(const std::string& str);
std::string toString(const std::wstring& wstr);
std::string toString(int x);

template <typename Arg>
inline std::string ToString(Arg const& arg) {
  return toString(arg);
}

template <typename... Args>
inline std::string ToString(Args const&... args) {
  std::ostringstream oss;
  int a[] = {0, ((void)(oss << ToString(args) << " "), 0)...};
  return oss.str();
}

template <typename... Args>
inline void Info(const Args... args) {
  std::cout << ToString(args...) << std::endl;
}

template <typename... Args>
inline void Warn(const Args... args) {
  std::cout << ToString(args...) << std::endl;
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
