#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/sinks/stdout_sinks.h>

#include "logging.h"

namespace trace {
std::shared_ptr<spdlog::logger> GetLogger() {
  auto logger = spdlog::get("dotnet-profiler");
  if (logger == nullptr) {
    logger = spdlog::rotating_logger_mt("dotnet-profiler", "C:\ProgramData\Datadog\logs",
                                        1024 * 1024 * 5, 3);
  }
  return logger;
}
}  // namespace trace
