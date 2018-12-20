#ifndef DD_CLR_PROFILER_PAL_H_
#define DD_CLR_PROFILER_PAL_H_

#ifdef _WIN32

#include <process.h>
#include <filesystem>
#include "windows.h"

#else

#include <unistd.h>
#include <fstream>

#endif

#include "environment_variables.h"
#include "string.h"  // NOLINT
#include "util.h"

namespace trace {

inline WSTRING DatadogLogFilePath() {
  WSTRING path = GetEnvironmentValue(environment::log_path);

  if (path.length() > 0) {
    return path;
  }

#ifdef _WIN32
  std::string programdata(getenv("PROGRAMDATA"));
  if (programdata.empty()) {
    programdata = "C:\\ProgramData";
  }
  return ToWSTRING(programdata +
                   "\\Datadog .NET Tracer\\logs\\dotnet-profiler.log");
#else
  return ToWSTRING("/var/log/datadog/dotnet-profiler.log");
#endif
}

inline WSTRING GetCurrentProcessName() {
#ifdef _WIN32
  WSTRING current_process_path(260, 0);
  const DWORD len = GetModuleFileName(nullptr, current_process_path.data(),
                                      (DWORD)(current_process_path.size()));
  current_process_path = current_process_path.substr(0, len);
  return std::filesystem::path(current_process_path).filename();
#else
  std::fstream comm("/proc/self/comm");
  std::string name;
  std::getline(comm, name);
  return ToWSTRING(name);
#endif
}

inline int GetPID() {
#ifdef _WIN32
  return _getpid();
#else
  return getpid();
#endif
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_PAL_H_
