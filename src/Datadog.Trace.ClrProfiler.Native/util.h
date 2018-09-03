#pragma once

#include <iterator>
#include <sstream>
#include <string>
#include <vector>

template <typename Out>
void split(const std::wstring &s, wchar_t delim, Out result) {
  std::wstringstream ss(s);
  std::wstring item;
  while (std::getline(ss, item, delim)) {
    *(result++) = item;
  }
}

inline std::vector<std::wstring> split(const std::wstring &s, wchar_t delim) {
  std::vector<std::wstring> elems;
  split(s, delim, std::back_inserter(elems));
  return elems;
}
