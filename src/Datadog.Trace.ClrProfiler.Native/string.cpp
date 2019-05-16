#include "string.h"
#include "miniutf.hpp"

namespace trace {

std::string ToString(const std::string& str) { return str; }
std::string ToString(const char* str) { return std::string(str); }
std::string ToString(const uint64_t i) {
  std::stringstream ss;
  ss << i;
  return ss.str();
}
std::string ToString(const ULONG i) {
  std::stringstream ss;
  ss << i;
  return ss.str();
}
std::string ToString(const WSTRING& wstr) {
  std::u16string ustr(reinterpret_cast<const char16_t*>(wstr.c_str()));
  return miniutf::to_utf8(ustr);
}

WSTRING ToWSTRING(const std::string& str) {
  auto ustr = miniutf::to_utf16(str);
  return WSTRING(reinterpret_cast<const WCHAR*>(ustr.c_str()));
}

WSTRING ToWSTRING(const uint64_t i) {
  const auto ustr = ToString(i);
  return ToWSTRING(ustr);
}

WSTRING ToWSTRING(const ULONG i) {
  const auto ustr = ToString(i);
  return ToWSTRING(ustr);
}

WCHAR operator"" _W(const char c) { return WCHAR(c); }

WSTRING operator"" _W(const char* arr, size_t size) {
  std::string str(arr, size);
  return ToWSTRING(str);
}

}  // namespace trace
