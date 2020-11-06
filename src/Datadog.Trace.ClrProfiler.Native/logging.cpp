#include "logging.h"

#include "pal.h"

namespace trace {

bool debug_logging_enabled = false;
bool dump_il_rewrite_enabled = false;

std::string Logger::GetLogPath() {
  const auto path = ToString(DatadogLogFilePath());

#ifdef _WIN32
  // on VC++, use std::filesystem (C++ 17) to
  // create directory if missing
  const auto log_path = std::filesystem::path(path);

  if (log_path.has_parent_path()) {
    const auto parent_path = log_path.parent_path();

    if (!std::filesystem::exists(parent_path)) {
      std::filesystem::create_directories(parent_path);
    }
  }
#endif

  return path;
}

Logger::Logger() {
  spdlog::set_error_handler([](const std::string& msg) {
    std::cerr << "Logger Handler: " << msg << std::endl;
  });

  spdlog::flush_every(std::chrono::seconds(3));

  m_fileout =
      spdlog::rotating_logger_mt("Logger", GetLogPath(), 1048576 * 5, 10);

  m_fileout->set_level(spdlog::level::debug);

  static auto current_process_name = ToString(GetCurrentProcessName());

  m_fileout->set_pattern("%D %I:%M:%S.%e %p [" + current_process_name +
                         "|%P|%t] [%l] %v");

  m_fileout->flush_on(spdlog::level::info);
};

Logger::~Logger() {
  m_fileout->flush();
  spdlog::shutdown();
};

void Logger::Debug(const std::string& str) {
  if (debug_logging_enabled) {
    m_fileout->debug(str);
  }
}
void Logger::Info(const std::string& str) { m_fileout->info(str); }
void Logger::Warn(const std::string& str) { m_fileout->warn(str); }
void Logger::Error(const std::string& str) { m_fileout->error(str); }
void Logger::Critical(const std::string& str) { m_fileout->critical(str); }
void Logger::Flush() { m_fileout->flush(); }
}  // namespace trace
