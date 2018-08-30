#include "util.h"

#include <cwctype>
#include <filesystem>
#include <iterator>
#include <sstream>
#include <string>
#include <vector>
#include "windows.h"

namespace trace {

template <typename Out>
void Split(const std::wstring &s, wchar_t delim, Out result) {
  std::wstringstream ss(s);
  std::wstring item;
  while (std::getline(ss, item, delim)) {
    *(result++) = item;
  }
}

std::vector<std::wstring> Split(const std::wstring &s, wchar_t delim) {
  std::vector<std::wstring> elems;
  Split(s, delim, std::back_inserter(elems));
  return elems;
}

std::wstring Trim(const std::wstring &str) {
  std::wstring trimmed = str;

  // trim space off the front
  while (trimmed.size() > 0 && std::iswspace(trimmed[0])) {
    trimmed = trimmed.substr(1);
  }

  // trim space off the back
  while (trimmed.size() > 0 && std::iswspace(trimmed[trimmed.size() - 1])) {
    trimmed = trimmed.substr(0, trimmed.size() - 1);
  }

  return trimmed;
}

std::wstring GetEnvironmentValue(const std::wstring &name) {
  const size_t max_buf_size = 4096;
  std::wstring buf(max_buf_size, 0);
  auto len =
      GetEnvironmentVariableW(name.data(), buf.data(), (DWORD)(buf.size()));
  return Trim(buf.substr(0, len));
}

std::vector<std::wstring> GetEnvironmentValues(const std::wstring &name,
                                               const wchar_t delim) {
  std::vector<std::wstring> values;
  for (auto &s : Split(GetEnvironmentValue(name), delim)) {
    s = Trim(s);
    if (!s.empty()) {
      values.push_back(s);
    }
  }
  return values;
}

std::vector<std::wstring> GetEnvironmentValues(const std::wstring &name) {
  return GetEnvironmentValues(name, ';');
}

std::wstring GetCurrentProcessName() {
  std::wstring current_process_path(260, 0);
  const DWORD len = GetModuleFileName(nullptr, current_process_path.data(),
                                      (DWORD)(current_process_path.size()));
  current_process_path = current_process_path.substr(0, len);
  return std::filesystem::path(current_process_path).filename();
}

}  // namespace trace
