#pragma once

#include <corhlpr.h>
#include <iomanip>
#include <locale>
#include <sstream>
#include <string>

#ifdef _WIN32
#define WStr(value) L##value
#define WStrLen(value) (size_t) wcslen(value)
#define WStrCmp(value1, value2) wcscmp(value1, value2)
#else
#define WStr(value) u##value
#define WStrLen(value) (size_t) std::char_traits<WCHAR>::length(value)
#define WStrCmp(value1, value2) std::char_traits<WCHAR>::compare(value1, value2, WStrLen(value1))
#endif

namespace shared
{
std::string PadLeft(const std::string& txt, std::size_t len, char c = ' ');

inline std::string Hex(ULONG value, int padding = 8, std::string prefix = "0x")
{
    std::stringstream str;
    str << prefix << std::hex << std::uppercase << std::right << std::setfill('0') << std::setw(padding) << (ULONG)value;
    return str.str();
}

typedef std::basic_string<WCHAR, std::char_traits<WCHAR>, std::allocator<WCHAR>> WSTRING;
#ifndef MACOS
typedef std::basic_stringstream<WCHAR, std::char_traits<WCHAR>, std::allocator<WCHAR>> WSTRINGSTREAM;
#else
// MACOS only support:
//    std::basic_stringstream<char, std::char_traits<char>, std::allocator<char>>
//    std::basic_stringstream<wchar_t, std::char_traits<wchar_t>, std::allocator<wchar_t>>
// If we try to use char16_t (WCHAR on non windows) then there's compilation errors when using the stringstream
// instance.
#endif

static WSTRING EmptyWStr = WStr("");
#if _WIN32
const static WSTRING EndLWStr = WStr("\r\n");
#else
const static WSTRING EndLWStr = WStr("\n");
#endif

std::string ToString(const std::string& str);
std::string ToString(const char* str);
std::string ToString(uint64_t i);
std::string ToString(const WSTRING& wstr);
std::string ToString(const WCHAR* wstr);
std::string ToString(const WCHAR* wstr, std::size_t nbChars);
std::string ToString(const GUID& uid);

WSTRING ToWSTRING(const std::string& str);
WSTRING ToWSTRING(uint64_t i);

bool TryParse(WSTRING const& s, int& result);

bool EndsWith(const std::string& str, const std::string& suffix);
bool StartsWith(const std::string& str, const std::string& prefix);

bool EndsWith(const WSTRING& str, const WSTRING& suffix);
bool StartsWith(const WSTRING& str, const WSTRING& prefix);

template <typename TChar>
std::basic_string<TChar> ReplaceString(std::basic_string<TChar> subject, const std::basic_string<TChar>& search,
                                       const std::basic_string<TChar>& replace)
{
    size_t pos = 0;
    while ((pos = subject.find(search, pos)) != std::basic_string<TChar>::npos)
    {
        subject.replace(pos, search.length(), replace);
        pos += replace.length();
    }
    return subject;
}

bool string_iequal(WSTRING const& s1, WSTRING const& s2);

} // namespace shared