#include "logging.h"

#include <spdlog/sinks/null_sink.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <filesystem>

namespace trace {
std::shared_ptr<spdlog::logger> GetLogger() {
  static std::mutex mtx;

  auto logger = spdlog::get("dotnet-profiler");
  if (logger == nullptr) {
    std::unique_lock<std::mutex> lck(mtx);

    try {
      char const* programdata = getenv("PROGRAMDATA");
      if (programdata == nullptr) {
        programdata = "C:\\ProgramData";
      }
      auto path = std::filesystem::path(programdata)
                      .append("Datadog")
                      .append("logs")
                      .append("dotnet-profiler.log");

      if (!std::filesystem::exists(path.parent_path())) {
        std::filesystem::create_directories(path.parent_path());
      }
      logger = spdlog::rotating_logger_mt("dotnet-profiler", path.string(),
                                          1024 * 1024 * 5, 3);

    } catch (...) {
      logger = spdlog::create<spdlog::sinks::null_sink_st>("dotnet-profiler");
    }
  }
  return logger;
}  // namespace trace
}  // namespace trace
