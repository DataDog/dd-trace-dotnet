#include "logging.h"

#include <iostream>

#include "util.h"

namespace trace {

std::string toString(const std::string& str) { return str; }
std::string toString(const std::wstring& wstr) {
  std::string str(wstr.begin(), wstr.end());
  return str;
}
std::string toString(int x) {
  std::ostringstream oss;
  oss << x;
  return oss.str();
}

}  // namespace trace
