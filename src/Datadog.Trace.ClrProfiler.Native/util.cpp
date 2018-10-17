#include "util.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string>
#include <vector>
#include "miniutf.hpp"
#include "pal.h"

namespace trace {

std::string toString(const std::string &str) { return str; }
std::string toString(const std::u16string &ustr) {
  return miniutf::to_utf8(ustr);
}
std::string toString(const std::wstring &wstr) {
  if (sizeof(char16_t) == sizeof(wchar_t)) {
    std::u16string ustr(reinterpret_cast<const char16_t *>(wstr.c_str()));
    return miniutf::to_utf8(ustr);
  } else {
    std::u32string ustr(reinterpret_cast<const char32_t *>(wstr.c_str()));
    return miniutf::to_utf8(ustr);
  }
}
std::string toString(int x) {
  std::stringstream s;
  s << x;
  return s.str();
}

std::wstring toWString(const std::string &str) {
  if (sizeof(char16_t) == sizeof(wchar_t)) {
    auto ustr = miniutf::to_utf16(str);
    std::wstring wstr(reinterpret_cast<const wchar_t *>(ustr.c_str()));
    return wstr;
  } else {
    auto ustr = miniutf::to_utf32(str);
    std::wstring wstr(reinterpret_cast<const wchar_t *>(ustr.c_str()));
    return wstr;
  }
}
std::wstring toWString(const std::u16string &ustr) {
  if (sizeof(char16_t) == sizeof(wchar_t)) {
    std::wstring wstr(reinterpret_cast<const wchar_t *>(ustr.c_str()));
    return wstr;
  } else {
    return toWString(miniutf::to_utf8(ustr));
  }
}
std::wstring toWString(const std::wstring &wstr) { return wstr; }
std::wstring toWString(int x) {
  std::wstringstream s;
  s << x;
  return s.str();
}

std::u16string toU16String(const std::wstring &wstr) {
  if (sizeof(char16_t) == sizeof(wchar_t)) {
    std::u16string ustr(reinterpret_cast<const char16_t *>(wstr.c_str()));
    return ustr;
  } else {
    std::u32string ustr(reinterpret_cast<const char32_t *>(wstr.c_str()));
    return miniutf::to_utf16(miniutf::to_utf8(ustr));
  }
}

template <typename Out>
void Split(const std::wstring &s, wchar_t delim, Out result) {
  size_t lpos = 0;
  for (size_t i = 0; i < s.length(); i++) {
    if (s[i] == delim) {
      *(result++) = s.substr(lpos, (i - lpos));
      lpos = i + 1;
    }
  }
  *(result++) = s.substr(lpos);
}

std::vector<std::wstring> Split(const std::wstring &s, wchar_t delim) {
  std::vector<std::wstring> elems;
  Split(s, delim, std::back_inserter(elems));
  return elems;
}

std::wstring Trim(const std::wstring &str) {
  if (str.length() == 0) {
    return L"";
  }

  std::wstring trimmed = str;

  auto lpos = trimmed.find_first_not_of(L" \t");
  if (lpos != std::wstring::npos) {
    trimmed = trimmed.substr(lpos);
  }

  auto rpos = trimmed.find_last_of(L" \t");
  if (rpos != std::wstring::npos) {
    trimmed = trimmed.substr(0, rpos);
  }

  return trimmed;
}

std::wstring GetEnvironmentValue(const std::wstring &name) {
#ifdef _WIN32
  const size_t max_buf_size = 4096;
  std::wstring buf(max_buf_size, 0);
  auto len =
      GetEnvironmentVariable(name.data(), buf.data(), (DWORD)(buf.size()));
  return Trim(buf.substr(0, len));
#else
  auto cstr = std::getenv(ToString(name).c_str());
  if (cstr == nullptr) {
    return L"";
  }
  std::string str(cstr);
  auto wstr = ToWString(str);
  return Trim(wstr);
#endif
}

std::vector<std::wstring> GetEnvironmentValues(const std::wstring &name,
                                               const wchar_t delim) {
  std::vector<std::wstring> values;
  for (auto s : Split(GetEnvironmentValue(name), delim)) {
    s = Trim(s);
    if (!s.empty()) {
      values.push_back(s);
    }
  }
  return values;
}

std::vector<std::wstring> GetEnvironmentValues(const std::wstring &name) {
  return GetEnvironmentValues(name, L';');
}

}  // namespace trace
