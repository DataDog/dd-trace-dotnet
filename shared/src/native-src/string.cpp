#include "string.h"
#ifdef _WIN32
#include <Windows.h>
#define tmp_buffer_size 512
#else
#include "miniutf.hpp"
#endif

namespace shared {

    std::string PadLeft(const std::string& txt, std::size_t len, char c)
    {
        std::stringstream str;
        str << std::right << std::setfill(c) << std::setw(len) << txt;
        return str.str();
    }

    std::string ToString(const std::string& str) { return str; }
    std::string ToString(const char* str) { return std::string(str); }
    std::string ToString(const uint64_t i) { return std::to_string(i); }

    std::string ToString(const WSTRING& wstr) { return ToString(wstr.data(), wstr.size()); }

    std::string ToString(const WCHAR* wstr)
    {
        if (wstr == nullptr || *wstr == WStr('\0')) return std::string();
        return ToString(wstr, WStrLen(wstr));
    }

    std::string ToString(const WCHAR* wstr, std::size_t nbChars)
    {
#ifdef _WIN32
        if (nbChars == 0) return std::string();

        char tmpStr[tmp_buffer_size];
        int charsWritten =
            WideCharToMultiByte(CP_UTF8, 0, wstr, (int)nbChars, tmpStr, tmp_buffer_size, nullptr, nullptr);
        if (charsWritten < tmp_buffer_size && charsWritten != 0)
        {
            return std::string(tmpStr, charsWritten);
        }

        // Get the actual size needed
        auto sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, wstr, (int)nbChars, nullptr, 0, nullptr, nullptr);

        std::string strTo(sizeNeeded, 0);
        WideCharToMultiByte(CP_UTF8, 0, wstr, (int)nbChars, strTo.data(), sizeNeeded, nullptr, nullptr);
        return strTo;
#else
        std::u16string ustr(reinterpret_cast<const char16_t*>(wstr), nbChars);
        return miniutf::to_utf8(ustr);
#endif
    }

    std::string ToString(const GUID& uid)
    {
        std::stringstream txt;
        //{821CFB0A-7847-490F-B8BC-F9E913BC2CA6}
        txt << "{";
        txt << Hex(uid.Data1, 8, "") << "-";
        txt << Hex(uid.Data2, 4, "") << "-";
        txt << Hex(uid.Data3, 4, "") << "-";
        txt << Hex(uid.Data4[0], 2, "") << Hex(uid.Data4[1], 2, "") << "-";
        txt << Hex(uid.Data4[2], 2, "") << Hex(uid.Data4[3], 2, "") << Hex(uid.Data4[4], 2, "")
            << Hex(uid.Data4[5], 2, "") << Hex(uid.Data4[6], 2, "") << Hex(uid.Data4[7], 2, "");
        txt << "}";
        return txt.str();
    }

    WSTRING ToWSTRING(const std::string& str) {
#ifdef _WIN32
        if (str.empty()) return std::wstring();

        wchar_t tmpStr[tmp_buffer_size];
        int charsWritten = MultiByteToWideChar(CP_UTF8, 0, str.data(), (int)str.size(), tmpStr, tmp_buffer_size);
        if (charsWritten < tmp_buffer_size && charsWritten != 0)
        {
            return std::wstring(tmpStr, charsWritten);
        }

        // Get the actual size needed
        auto sizeNeeded = MultiByteToWideChar(CP_UTF8, 0, str.data(), (int)str.size(), nullptr, 0);

        std::wstring wstrTo(sizeNeeded, 0);
        MultiByteToWideChar(CP_UTF8, 0, str.data(), (int)str.size(), wstrTo.data(), sizeNeeded);
        return wstrTo;
#else
        auto ustr = miniutf::to_utf16(str);
        return WSTRING(reinterpret_cast<const WCHAR*>(ustr.c_str()));
#endif
    }

    // taken from https://chromium.googlesource.com/chromium/src/base/+/refs/heads/main/strings/string_number_conversions_internal.h
    // static STR IntToStringT(INT value)
    // simplified for our case
    WSTRING ToWSTRING(const uint64_t value) {
        // log10(2) ~= 0.3 bytes needed per bit or per byte log10(2**8) ~= 2.4.
        const size_t bufferSize = 3 * sizeof(uint64_t);

        // Create the string in a temporary buffer, write it back to front, and
        // then return the substr of what we ended up using.
        WCHAR outbuf[bufferSize];
        WCHAR* end = outbuf + bufferSize;
        WCHAR* i = end;
        auto res = value;
        do
        {
            --i;
            *i = static_cast<WCHAR>((res % 10) + '0');
            res /= 10;
        } while (res != 0);
        return WSTRING(i, end);
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

    bool EndsWith(const WSTRING& str, const WSTRING& suffix)
    {
        return str.size() >= suffix.size() && str.compare(str.size() - suffix.size(), suffix.size(), suffix) == 0;
    }

    bool StartsWith(const std::string &str, const std::string &prefix)
    {
        return str.size() >= prefix.size() && str.compare(0, prefix.size(), prefix) == 0;
    }

    bool StartsWith(const WSTRING &str, const WSTRING &prefix)
    {
        return str.size() >= prefix.size() && str.compare(0, prefix.size(), prefix) == 0;
    }

    bool icompare_pred(WCHAR a, WCHAR b)
    {
        return std::tolower(a) == std::tolower(b);
    }

    bool string_iequal(WSTRING const& s1, WSTRING const& s2)
    {
        if (s1.length() == s2.length())
        {
            return std::equal(s2.begin(), s2.end(), s1.begin(), icompare_pred);
        }

        return false;
    }
}  // namespace trace