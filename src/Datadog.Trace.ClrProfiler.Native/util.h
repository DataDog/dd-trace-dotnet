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

// GetEnvironmentValues returns environment variable values for the given name
// split by the delimiter. Space is trimmed and empty values are ignored.
std::vector<WSTRING> GetEnvironmentValues(const WSTRING &name,
                                          const wchar_t delim);

// GetEnvironmentValues calls GetEnvironmentValues with a semicolon delimiter.
std::vector<WSTRING> GetEnvironmentValues(const WSTRING &name);

template <class Container>
bool Contains(const Container &items,
              const typename Container::value_type &value) {
  return std::find(items.begin(), items.end(), value) != items.end();
}

// Singleton definition
class UnCopyable {
 protected:
  UnCopyable(){};
  ~UnCopyable(){};

 private:
  UnCopyable(const UnCopyable &) = delete;
  UnCopyable(const UnCopyable &&) = delete;
  UnCopyable &operator=(const UnCopyable &) = delete;
  UnCopyable &operator=(const UnCopyable &&) = delete;
};

template <typename T>
class Singleton : public UnCopyable {
 public:
  static T *Instance() {
    static T instance_obj;
    return &instance_obj;
  }
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_UTIL_H_
