#ifndef DD_CLR_PROFILER_STDAFX_H_
#define DD_CLR_PROFILER_STDAFX_H_

#include <cor.h>
#include <corhlpr.h>
#include <corprof.h>
#undef ERROR

#include <glog/logging.h>

// glog wstring support

#include <wchar.h>

#include <iostream>
#include <string>

inline std::ostream& operator<<(std::ostream& out, const wchar_t* str) {
  size_t len = wcsrtombs(NULL, &str, 0, NULL);
  char* buf = (char*)malloc(len + 1);
  buf[len] = 0;
  wcsrtombs(buf, &str, len, NULL);
  out << buf;
  free(buf);
  return out;
}

inline std::ostream& operator<<(std::ostream& out, const std::wstring& str) {
  return operator<<(out, str.c_str());
}

#endif  // DD_CLR_PROFILER_STDAFX_H_