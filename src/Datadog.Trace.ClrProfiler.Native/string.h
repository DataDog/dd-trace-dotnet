#ifndef DD_CLR_PROFILER_STRING_H_
#define DD_CLR_PROFILER_STRING_H_

#include <corhlpr.h>
#include <sstream>
#include <string>

namespace trace {

typedef std::basic_string<WCHAR> WSTRING;

std::string ToString(const std::string& str);
std::string ToString(const char* str);
std::string ToString(const uint64_t i);
std::string ToString(const WSTRING& wstr);

WSTRING ToWSTRING(const std::string& str);
WSTRING ToWSTRING(const uint64_t i);

WCHAR operator"" _W(const char c);
WSTRING operator"" _W(const char* arr, size_t size);

}  // namespace trace

#endif  // DD_CLR_PROFILER_STRING_H_
