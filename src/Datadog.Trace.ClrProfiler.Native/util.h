#ifndef DD_CLR_PROFILER_UTIL_H_
#define DD_CLR_PROFILER_UTIL_H_

#include <sstream>
#include <string>
#include <vector>

#include "string.h"

namespace trace {

template <typename Out>
void Split(const WSTRING &s, wchar_t delim, Out result);

// Split splits a string by the given delimiter.
std::vector<WSTRING> Split(const WSTRING &s, wchar_t delim);

// Trim removes space from the beginning and end of a string.
WSTRING Trim(const WSTRING &str);

// GetEnvironmentValue returns the environment variable value for the given
// name. Space is trimmed.
WSTRING GetEnvironmentValue(const WSTRING &name);

// GetEnvironmentValues returns environment variable values for the given name
// split by the delimiter. Space is trimmed and empty values are ignored.
std::vector<WSTRING> GetEnvironmentValues(const WSTRING &name,
                                          const wchar_t delim);

// GetEnvironmentValues calls GetEnvironmentValues with a semicolon delimiter.
std::vector<WSTRING> GetEnvironmentValues(const WSTRING &name);

}  // namespace trace

#endif  // DD_CLR_PROFILER_UTIL_H_
