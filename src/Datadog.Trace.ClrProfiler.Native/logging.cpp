#include <spdlog/sinks/null_sink.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <filesystem>

#include "logging.h"
#include "util.h"

namespace trace {
std::shared_ptr<spdlog::logger> GetLogger() {
  static std::mutex mtx;

  auto logger = spdlog::get("dotnet-profiler");
  if (logger == nullptr) {
    std::unique_lock<std::mutex> lck(mtx);

    try {
      const auto logger_enabled =
          GetEnvironmentValue(L"DD_DOTNET_TRACER_LOG_ENABLED");

      if (logger_enabled == L"0") {
        // disable logging, useful if the logger itself is crashing
        logger = spdlog::create<spdlog::sinks::null_sink_st>("dotnet-profiler");
      } else {
        auto programdata = GetEnvironmentValue(L"PROGRAMDATA");
        if (programdata.empty()) {
          programdata = L"C:\\ProgramData";
        }

        DWORD process_id = GetCurrentProcessId();

        auto path = std::filesystem::path(programdata)
                        .append("Datadog .NET Tracer")
                        .append("logs")
                        .append("dotnet-profiler-" + std::to_string(process_id) + ".log");

        if (!std::filesystem::exists(path.parent_path())) {
          std::filesystem::create_directories(path.parent_path());
        }
        logger = spdlog::rotating_logger_mt("dotnet-profiler", path.string(),
                                            1024 * 1024 * 5, 3);
      }
    } catch (...) {
      logger = spdlog::create<spdlog::sinks::null_sink_st>("dotnet-profiler");
    }
  }
  return logger;
}  // namespace trace
}  // namespace trace
