#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include <cmath>

#include <spdlog/spdlog.h>
#include <codecvt>
#include <iostream>
#include <locale>

namespace trace {
std::shared_ptr<spdlog::logger> GetLogger();
}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
