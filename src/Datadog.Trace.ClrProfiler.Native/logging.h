#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_
#include "util.h"

#include <spdlog/spdlog.h>

#include <iostream>
#include <memory>

namespace trace {

extern bool debug_logging_enabled;
extern bool dump_il_rewrite_enabled;

class Logger : public Singleton<Logger> {
  friend class Singleton<Logger>;

 private:
  std::shared_ptr<spdlog::logger> m_fileout;
  static std::string GetLogPath(const std::string& file_name_suffix);
  Logger();
  ~Logger();

 public:
  void Debug(const std::string& str);
  void Info(const std::string& str);
  void Warn(const std::string& str);
  void Error(const std::string& str);
  void Critical(const std::string& str);
  void Flush();
  static void Shutdown() { spdlog::shutdown(); }
};

template <typename Arg>
std::string LogToString(Arg const& arg) {
  return ToString(arg);
}

template <typename... Args>
std::string LogToString(Args const&... args) {
  std::ostringstream oss;
  int a[] = {0, ((void)(oss << LogToString(args)), 0)...};
  return oss.str();
}

template <typename... Args>
void Debug(const Args... args) {
  Logger::Instance()->Debug(LogToString(args...));
}

template <typename... Args>
void Info(const Args... args) {
  Logger::Instance()->Info(LogToString(args...));
}

template <typename... Args>
void Warn(const Args... args) {
  Logger::Instance()->Warn(LogToString(args...));
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
