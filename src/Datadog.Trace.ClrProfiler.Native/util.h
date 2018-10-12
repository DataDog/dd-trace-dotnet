#ifndef DD_CLR_PROFILER_UTIL_H_
#define DD_CLR_PROFILER_UTIL_H_

#include <array>
#include <codecvt>
#include <locale>
#include <string>
#include <vector>

namespace trace {

template <typename Out>
void Split(const std::u16string &s, char16_t delim, Out result);

// Split splits a string by the given delimiter.
std::vector<std::u16string> Split(const std::u16string &s, char16_t delim);

// Trim removes space from the beginning and end of a string.
std::u16string Trim(const std::u16string &str);

// GetEnvironmentValue returns the environment variable value for the given
// name. Space is trimmed.
std::u16string GetEnvironmentValue(const std::u16string &name);

// GetEnvironmentValues returns environment variable values for the given name
// split by the delimiter. Space is trimmed and empty values are ignored.
std::vector<std::u16string> GetEnvironmentValues(const std::u16string &name,
                                                 const char16_t delim);

// GetEnvironmentValues calls GetEnvironmentValues with a semicolon delimiter.
std::vector<std::u16string> GetEnvironmentValues(const std::u16string &name);

// GetCurrentProcessName gets the current process file name.
std::u16string GetCurrentProcessName();

std::u16string ToU16(const std::string &str);

std::u16string ToU16(const std::wstring &wstr);

std::string ToU8(const std::u16string &ustr);

std::string ToU8(const std::wstring &wstr);

std::wstring ToW(const std::string &str);

std::wstring ToW(const std::u16string &ustr);

wchar_t* ToLPWSTR(const std::u16string &ustr);

bool IsSpace(const char16_t c);

}  // namespace trace

#endif  // DD_CLR_PROFILER_UTIL_H_
