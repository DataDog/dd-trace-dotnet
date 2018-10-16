#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include <iostream>

#include "util.h"

namespace trace {

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
