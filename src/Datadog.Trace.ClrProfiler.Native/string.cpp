#include "string.h"
#ifdef _WIN32
#include <Windows.h>
#define tmp_buffer_size 512
#else
#include "miniutf.hpp"
#endif

namespace trace {

std::string ToString(const std::string& str) { return str; }
std::string ToString(const char* str) { return std::string(str); }
std::string ToString(const uint64_t i) { return std::to_string(i); }
std::string ToString(const WSTRING& wstr) {
#ifdef _WIN32
  if (wstr.empty()) return std::string();

  std::string tmpStr(tmp_buffer_size, 0);
  int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &tmpStr[0], tmp_buffer_size, NULL, NULL);
  if (size_needed < tmp_buffer_size) {
    return tmpStr.substr(0, size_needed);
  }

  std::string strTo(size_needed, 0);
  WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
  return strTo;
#else
  std::u16string ustr(reinterpret_cast<const char16_t*>(wstr.c_str()));
  return miniutf::to_utf8(ustr);
#endif
}

WSTRING ToWSTRING(const std::string& str) {
#ifdef _WIN32
  if (str.empty()) return std::wstring();

  std::wstring tmpStr(tmp_buffer_size, 0);
  int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), &tmpStr[0], tmp_buffer_size);
  if (size_needed < tmp_buffer_size) {
    return tmpStr.substr(0, size_needed);
  }

  std::wstring wstrTo(size_needed, 0);
  MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), &wstrTo[0], size_needed);
  return wstrTo;
#else
  auto ustr = miniutf::to_utf16(str);
  return WSTRING(reinterpret_cast<const WCHAR*>(ustr.c_str()));
#endif
}

WSTRING ToWSTRING(const uint64_t i) {
  return WSTRING(reinterpret_cast<const WCHAR*>(std::to_wstring(i).c_str()));
}

}  // namespace trace