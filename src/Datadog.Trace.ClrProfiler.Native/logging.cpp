#include "logging.h"

#include <unistd.h>
#include <fstream>
#include <ios>
#include <sstream>

namespace trace {

std::string LogName() { return "/var/log/datadog/dotnet-profiler.log"; }

void Log(const std::string& str) {
  try {
    std::ofstream out(LogName(), std::ios::app);
    std::stringstream ss;
    ss << "[pid:" << getpid() << "] " << str << std::endl;
    out << ss.str();
  } catch (...) {
  }
}

}  // namespace trace
