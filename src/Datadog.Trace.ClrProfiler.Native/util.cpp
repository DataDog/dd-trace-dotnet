#include "util.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string>
#include <vector>
#include "miniutf.hpp"
#include "pal.h"

namespace trace {

template <typename Out>
void Split(const WSTRING &s, wchar_t delim, Out result) {
  size_t lpos = 0;
  for (size_t i = 0; i < s.length(); i++) {
    if (s[i] == delim) {
      *(result++) = s.substr(lpos, (i - lpos));
      lpos = i + 1;
    }
  }
  *(result++) = s.substr(lpos);
}

std::vector<WSTRING> Split(const WSTRING &s, wchar_t delim) {
  std::vector<WSTRING> elems;
  Split(s, delim, std::back_inserter(elems));
  return elems;
}

WSTRING Trim(const WSTRING &str) {
  if (str.length() == 0) {
    return ""_W;
  }

  WSTRING trimmed = str;

  auto lpos = trimmed.find_first_not_of(" \t"_W);
  if (lpos != WSTRING::npos && lpos > 0) {
    trimmed = trimmed.substr(lpos);
  }

  auto rpos = trimmed.find_last_not_of(" \t"_W);
  if (rpos != WSTRING::npos) {
    trimmed = trimmed.substr(0, rpos + 1);
  }

  return trimmed;
}

WSTRING GetEnvironmentValue(const WSTRING &name) {
#ifdef _WIN32
  const size_t max_buf_size = 4096;
  WSTRING buf(max_buf_size, 0);
  auto len =
      GetEnvironmentVariable(name.data(), buf.data(), (DWORD)(buf.size()));
  return Trim(buf.substr(0, len));
#else
  auto cstr = std::getenv(ToString(name).c_str());
  if (cstr == nullptr) {
    return ""_W;
  }
  std::string str(cstr);
  auto wstr = ToWSTRING(str);
  return Trim(wstr);
#endif
}

std::vector<WSTRING> GetEnvironmentValues(const WSTRING &name,
                                          const wchar_t delim) {
  std::vector<WSTRING> values;
  for (auto s : Split(GetEnvironmentValue(name), delim)) {
    s = Trim(s);
    if (!s.empty()) {
      values.push_back(s);
    }
  }
  return values;
}

std::vector<WSTRING> GetEnvironmentValues(const WSTRING &name) {
  return GetEnvironmentValues(name, L';');
}

bool Contains(const std::vector<WSTRING>& items, const WSTRING& value) {
  return !items.empty() &&
         std::find(items.begin(), items.end(), value) != items.end();
}

} // namespace trace
