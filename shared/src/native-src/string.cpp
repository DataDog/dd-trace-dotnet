#include "string.h"
#ifdef _WIN32
#include <Windows.h>
#define tmp_buffer_size 512
#else
#include "miniutf.hpp"
#endif

namespace shared {

    std::string ToString(const std::string& str) { return str; }
    std::string ToString(const char* str) { return std::string(str); }
    std::string ToString(const uint64_t i) { return std::to_string(i); }

    std::string ToString(const WSTRING& wstr) { return ToString(wstr.data(), wstr.size()); }

    std::string ToString(const WCHAR* wstr, std::size_t nbChars)
    {
#ifdef _WIN32
        if (nbChars == 0) return std::string();

        char tmpStr[tmp_buffer_size] = {0};
        int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)nbChars, &tmpStr[0], tmp_buffer_size, NULL, NULL);
        if (size_needed < tmp_buffer_size)
        {
            return std::string(tmpStr, size_needed);
        }

        std::string strTo(size_needed, 0);
        WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)nbChars, &strTo[0], size_needed, NULL, NULL);
        return strTo;
#else
        std::u16string ustr(reinterpret_cast<const char16_t*>(wstr), nbChars);
        return miniutf::to_utf8(ustr);
#endif
    }

    WSTRING ToWSTRING(const std::string& str) {
#ifdef _WIN32
        if (str.empty()) return std::wstring();

        wchar_t tmpStr[tmp_buffer_size] = {0};
        int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), &tmpStr[0], tmp_buffer_size);
        if (size_needed < tmp_buffer_size) {
            return std::wstring(tmpStr, size_needed);
        }

        std::wstring wstrTo(size_needed, 0);
        MultiByteToWideChar(CP_UTF8, 0, &str[0], (int)str.size(), &wstrTo[0], size_needed);
        return wstrTo;
#else
        auto ustr = miniutf::to_utf16(str);
        return WSTRING(reinterpret_cast<const WCHAR*>(ustr.c_str()));
#endif
    }

    WSTRING ToWSTRING(const uint64_t i) {
        return WSTRING(reinterpret_cast<const WCHAR*>(std::to_wstring(i).c_str()));
    }

    bool TryParse(WSTRING const& s, int& result) {

        if (s.empty())
        {
            result = 0;
            return false;
        }
        try
        {
            result = std::stoi(ToString(s));
            return true;
        }
        catch (std::exception const&)
        {
        }

        result = 0;
        return false;
    }

    bool EndsWith(const std::string& str, const std::string& suffix)
    {
        return str.size() >= suffix.size() && str.compare(str.size() - suffix.size(), suffix.size(), suffix) == 0;
    }

    bool StartsWith(const std::string &str, const std::string &prefix)
    {
        return str.size() >= prefix.size() && str.compare(0, prefix.size(), prefix) == 0;
    }

}  // namespace trace