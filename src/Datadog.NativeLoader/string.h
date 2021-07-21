#pragma once
#include <corhlpr.h>
#include <sstream>
#include <string>
#include <vector>

#ifdef _WIN32
#define WStr(value) L##value
#define WStrLen(value) (size_t) wcslen(value)
#else
#define WStr(value) u##value
#define WStrLen(value) (size_t) std::char_traits<char16_t>::length(value)
#endif

typedef std::basic_string<WCHAR> WSTRING;

#ifndef MACOS
typedef std::basic_stringstream<WCHAR> WSTRINGSTREAM;
#endif

std::string ToString(const std::string& str);
std::string ToString(const char* str);
std::string ToString(const uint64_t i);
std::string ToString(const WSTRING& wstr);
std::string ToString(const LPTSTR& tstr);

WSTRING ToWSTRING(const std::string& str);
WSTRING ToWSTRING(const uint64_t i);

WSTRING HexStr(const void* data, int len);

// Trim removes space from the beginning and end of a string.
WSTRING Trim(const WSTRING& str);

// Trim removes space from the beginning and end of a string.
std::string Trim(const std::string& str);

// Split splits a string by the given delimiter.
std::vector<WSTRING> Split(const WSTRING& s, wchar_t delim);

// Split splits a string by the given delimiter.
std::vector<std::string> Split(const std::string& s, char delim);
