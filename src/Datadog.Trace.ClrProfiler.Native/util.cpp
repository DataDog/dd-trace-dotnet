#include "util.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string>
#include <vector>

#include <corhlpr.h>

namespace trace {

template <typename Out>
void Split(const std::u16string &s, char16_t delim, Out result) {
  size_t next = 0;
  size_t prev = 0;
  while ((next = s.find(delim, prev)) != std::u16string::npos) {
    *(result++) = s.substr(prev, next - prev);
    prev = next;
  }
  *(result++) = s.substr(prev);
}

std::vector<std::u16string> Split(const std::u16string &s, char16_t delim) {
  std::vector<std::u16string> elems;
  Split(s, delim, std::back_inserter(elems));
  return elems;
}

std::u16string Trim(const std::u16string &str) {
  std::u16string trimmed = str;

  // trim space off the front
  while (!trimmed.empty() && IsSpace(trimmed[0])) {
    trimmed = trimmed.substr(1);
  }

  // trim space off the back
  while (!trimmed.empty() && IsSpace(trimmed[trimmed.size() - 1])) {
    trimmed = trimmed.substr(0, trimmed.size() - 1);
  }

  return trimmed;
}

std::u16string GetEnvironmentValue(const std::u16string &name) {
  const size_t max_buf_size = 4096;
  std::u16string buf(max_buf_size, 0);
  auto len = GetEnvironmentVariable(ToLPWSTR(name), ToLPWSTR(buf),
                                    (DWORD)(buf.size()));
  return Trim(buf.substr(0, len));
}

std::vector<std::u16string> GetEnvironmentValues(const std::u16string &name,
                                                 const char16_t delim) {
  std::vector<std::u16string> values;
  for (auto &s : Split(GetEnvironmentValue(name), delim)) {
    s = Trim(s);
    if (!s.empty()) {
      values.push_back(s);
    }
  }
  return values;
}

std::vector<std::u16string> GetEnvironmentValues(const std::u16string &name) {
  return GetEnvironmentValues(name, u';');
}

std::u16string GetCurrentProcessName() {
  std::u16string current_process_path(260, 0);
  const DWORD len = GetModuleFileName(nullptr, ToLPWSTR(current_process_path),
                                      (DWORD)(current_process_path.size()));
  current_process_path = current_process_path.substr(0, len);

  auto idx = current_process_path.find(u"\\");
  if (idx != std::u16string::npos) {
    current_process_path = current_process_path.substr(idx + 1);
  }

  idx = current_process_path.find(u"/");
  if (idx != std::u16string::npos) {
    current_process_path = current_process_path.substr(idx + 1);
  }

  return current_process_path;
}

std::u16string ToU16(const std::string &str) { return ToU16(ToW(str)); }

std::u16string ToU16(const std::wstring &wstr) {
#ifdef _WIN32
  return std::u16string(reinterpret_cast<const char16_t *>(wstr.data()));
#else
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>, wchar_t> convert_from;
  std::wstring_convert<std::codecvt_utf8_utf16<char16_t>, char16_t> convert_to;
  auto bstr = convert_from.to_bytes(wstr);
  return convert_to.from_bytes(bstr);
#endif
}

std::string ToU8(const std::u16string &ustr) { return ToU8(ToW(ustr)); }

std::string ToU8(const std::wstring &wstr) {
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>, wchar_t> converter;
  return converter.to_bytes(wstr);
}

std::wstring ToW(const std::string &str) {
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>, wchar_t> converter;
  return converter.from_bytes(str);
}

std::wstring ToW(const std::u16string &ustr) {
#ifdef _WIN32
  return std::wstring(reinterpret_cast<const wchar_t *>(ustr.data()));
#else
  std::wstring_convert<std::codecvt_utf8_utf16<char16_t>, char16_t>
      convert_from;
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>, wchar_t> convert_to;
  auto bstr = convert_from.to_bytes(ustr);
  return convert_to.from_bytes(bstr);
#endif
}

#ifdef _WIN32
wchar_t *ToLPWSTR(const std::u16string &ustr) {
  return reinterpret_cast<wchar_t *>(const_cast<char16_t *>(ustr.data()));
}
#else
char16_t *ToLPWSTR(const std::u16string &ustr) {
  return const_cast<char16_t *>(ustr.data());
}
#endif

bool IsSpace(const char16_t c) {
  return c == u' ' || c == u'\t' || c == u'\r' || c == u'\n' || c == u'\v';
}

}  // namespace trace
