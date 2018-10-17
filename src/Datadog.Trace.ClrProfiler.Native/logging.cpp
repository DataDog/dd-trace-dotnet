#include "logging.h"

#include <fstream>
#include <ios>
#include <sstream>

#include "pal.h"

namespace trace {

void Log(const std::string& str) {
  static auto current_process_name = ToString(GetCurrentProcessName());

  std::stringstream ss;
  ss << "[" << current_process_name << "] " << GetPID() << ": " << str
     << std::endl;
  auto line = ss.str();

  try {
    std::ofstream out(DatadogLogFilePath(), std::ios::app);
    out << line;
  } catch (...) {
  }
}

}  // namespace trace
