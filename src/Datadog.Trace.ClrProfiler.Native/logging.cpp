#include "logging.h"

#include <fstream>
#include <ios>
#include <sstream>

#include "pal.h"

namespace trace {

void Log(const std::string& str) {
  try {
    std::ofstream out(DatadogLogFilePath(), std::ios::app);
    std::stringstream ss;
    ss << "[pid:" << GetPID() << "] " << str << std::endl;
    out << ss.str();
  } catch (...) {
  }
}

}  // namespace trace
