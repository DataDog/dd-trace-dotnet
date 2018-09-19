#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/sinks/stdout_sinks.h>
#include <filesystem>

#include "logging.h"
#include "util.h"

namespace trace {
std::shared_ptr<spdlog::logger> GetLogger() {
  static std::mutex mtx;

  auto logger = spdlog::get("dotnet-profiler");
  if (logger == nullptr) {
    std::unique_lock<std::mutex> lck(mtx);

    std::string logfile = R"(C:\ProgramData\Datadog\logs\profiler.log)";

    const auto programdata = GetEnvironmentValue(L"PROGRAMDATA");
    const auto dddir = std::filesystem::path(programdata).append("Datadog");
    if (std::filesystem::exists(dddir)) {
      logfile = dddir.string();
    }

    logger = spdlog::rotating_logger_mt("dotnet-profiler", logfile,
                                        1024 * 1024 * 5, 3);
  }
  return logger;
}
}  // namespace trace
