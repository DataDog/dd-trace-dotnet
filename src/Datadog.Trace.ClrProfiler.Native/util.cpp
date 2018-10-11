#include "util.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string>
#include <vector>
#include "pal.h"

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
  auto len =
      GetEnvironmentVariable(name.data(), buf.data(), (DWORD)(buf.size()));
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
  const DWORD len = GetModuleFileName(nullptr, current_process_path.data(),
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

}  // namespace trace
