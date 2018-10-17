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

namespace trace {

inline std::string DatadogLogFilePath() {
#ifdef _WIN32
  std::string programdata(getenv("PROGRAMDATA"));
  if (programdata.empty()) {
    programdata = "C:\\ProgramData";
  }
  return programdata + "\\Datadog\\logs\\dotnet-profiler.log";
#else
  return "/var/log/datadog/dotnet-profiler.log";
#endif
}

inline std::wstring GetCurrentProcessName() {
#ifdef _WIN32
  std::wstring current_process_path(260, 0);
  const DWORD len = GetModuleFileName(nullptr, current_process_path.data(),
                                      (DWORD)(current_process_path.size()));
  current_process_path = current_process_path.substr(0, len);
  return std::filesystem::path(current_process_path).filename();
#else
  std::wfstream comm("/proc/self/comm");
  std::wstring name;
  std::getline(comm, name);
  return name;
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
