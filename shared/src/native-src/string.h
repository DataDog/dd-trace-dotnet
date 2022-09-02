#pragma once

#include <corhlpr.h>
#include <locale>
#include <sstream>
#include <string>

#ifdef _WIN32
#define WStr(value) L##value
#define WStrLen(value) (size_t) wcslen(value)
#else
#define WStr(value) u##value
#define WStrLen(value) (size_t) std::char_traits<WCHAR>::length(value)
#endif

namespace shared {

    typedef std::basic_string<WCHAR, std::char_traits<WCHAR>, std::allocator<WCHAR>> WSTRING;
#ifndef MACOS
    typedef std::basic_stringstream<WCHAR, std::char_traits<WCHAR>, std::allocator<WCHAR>> WSTRINGSTREAM;
#else
    // MACOS only support:
    //    std::basic_stringstream<char, std::char_traits<char>, std::allocator<char>>
    //    std::basic_stringstream<wchar_t, std::char_traits<wchar_t>, std::allocator<wchar_t>>
    // If we try to use char16_t (WCHAR on non windows) then there's compilation errors when using the stringstream instance.
#endif

    static WSTRING EmptyWStr = WStr("");
#if _WIN32
    const static WSTRING EndLWStr = WStr("\r\n");
#else
    const static WSTRING EndLWStr = WStr("\n");
#endif

    std::string ToString(const std::string& str);
    std::string ToString(const char* str);
    std::string ToString(const uint64_t i);
    std::string ToString(const WSTRING& wstr);
    std::string ToString(const WCHAR* wstr, std::size_t nbChars);

    WSTRING ToWSTRING(const std::string& str);
    WSTRING ToWSTRING(const uint64_t i);

    bool TryParse(WSTRING const& s, int& result);

    bool EndsWith(const std::string& str, const std::string& suffix);

    bool StartsWith(const std::string &str, const std::string &prefix);

    template <typename TChar>
    std::basic_string<TChar> ReplaceString(std::basic_string<TChar> subject, const std::basic_string<TChar>& search, const std::basic_string<TChar>& replace) {
        size_t pos = 0;
        while ((pos = subject.find(search, pos)) != std::basic_string<TChar>::npos) {
            subject.replace(pos, search.length(), replace);
            pos += replace.length();
        }
        return subject;
    }

}  // namespace shared