#ifndef DD_TRACE_UTIL_H_
#define DD_TRACE_UTIL_H_

#include <string>

const std::wstring ToWString(const std::string& str) {
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  return converter.from_bytes(str);
}

#endif // DD_TRACE_UTIL_H_
