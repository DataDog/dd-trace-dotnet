#include "logging.h"

#include "pal.h"

#include "spdlog/sinks/null_sink.h"
#include "spdlog/sinks/basic_file_sink.h"

#ifndef _WIN32
typedef struct stat Stat;
#endif

namespace trace {

bool debug_logging_enabled = false;
bool dump_il_rewrite_enabled = false;

#ifndef _WIN32
// for linux and osx we need a function to get the path from a filepath
std::string getPathName(const std::string& s) {
  char sep = '/';
  size_t i = s.rfind(sep, s.length());
  if (i != std::string::npos) {
    return s.substr(0, i);
  }
  return "";
}
#endif

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
#else
  // on linux and osx we use the basic C approach
  const auto log_path = getPathName(path);
  Stat st;
  if (log_path != "" && stat(log_path.c_str(), &st) != 0) {
    mkdir(log_path.c_str(), 0777);
  }
#endif

  return path;
}

Logger::Logger() {
  spdlog::set_error_handler([](const std::string& msg) {
    std::cerr << "Logger Handler: " << msg << std::endl;
  });

  spdlog::flush_every(std::chrono::seconds(3));

  try {
    m_fileout = spdlog::basic_logger_mt("Logger", GetLogPath());
  }
  catch (...) {
    std::cerr << "Logger Handler: Error creating native log file." << std::endl;
    m_fileout = spdlog::null_logger_mt("Logger");
  }

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
