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
    const auto path = ToString(DatadogLogFilePath());
    const auto log_path = std::filesystem::path(path);

    if (log_path.has_parent_path()) {
      const auto parent_path = log_path.parent_path();

      if (!std::filesystem::exists(parent_path)) {
        std::filesystem::create_directories(parent_path);
      }
    }

    std::ofstream out(path, std::ios::app);
    out << line;
  } catch (...) {
  }
}

}  // namespace trace
