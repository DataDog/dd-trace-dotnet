#ifndef DD_CLR_PROFILER_UTIL_H_
#define DD_CLR_PROFILER_UTIL_H_

#include <algorithm>
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

// SplitAndTrim splits the input string by the delimiter, additionally
// trimming space and ignoring empty values.
std::vector<WSTRING> SplitAndTrim(const WSTRING &delimited_values,
                               const wchar_t delim);

// SplitAndTrim calls SplitAndTrim with a semicolon delimiter.
std::vector<WSTRING> SplitAndTrim(const WSTRING &delimited_values);

template <class Container>
bool Contains(const Container &items,
              const typename Container::value_type &value) {
  return std::find(items.begin(), items.end(), value) != items.end();
}
}  // namespace trace

#endif  // DD_CLR_PROFILER_UTIL_H_
