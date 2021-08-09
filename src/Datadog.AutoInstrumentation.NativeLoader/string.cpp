#include "string.h"
#ifdef _WIN32
#include <Windows.h>
#define tmp_buffer_size 512
#else
#include "miniutf.hpp"
#endif

std::string ToString(const std::string& str)
{
    return str;
}
std::string ToString(const char* str)
{
    return std::string(str);
}
std::string ToString(const uint64_t i)
{
    return std::to_string(i);
}
std::string ToString(const WSTRING& wstr)
{
#ifdef _WIN32
    if (wstr.empty()) return std::string();

    std::string tmpStr(tmp_buffer_size, 0);
    int size_needed =
        WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int) wstr.size(), &tmpStr[0], tmp_buffer_size, NULL, NULL);
    if (size_needed < tmp_buffer_size)
    {
        return tmpStr.substr(0, size_needed);
    }

    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int) wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
#else
    std::u16string ustr(reinterpret_cast<const char16_t*>(wstr.c_str()));
    return miniutf::to_utf8(ustr);
#endif
}
std::string ToString(const LPTSTR& tstr)
{
#if defined(_UNICODE)
    return ToString(WSTRING(tstr));
#else
    return std::string((char*)tstr);
#endif
}

WSTRING ToWSTRING(const std::string& str)
{
#ifdef _WIN32
    if (str.empty()) return std::wstring();

    std::wstring tmpStr(tmp_buffer_size, 0);
    int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], (int) str.size(), &tmpStr[0], tmp_buffer_size);
    if (size_needed < tmp_buffer_size)
    {
        return tmpStr.substr(0, size_needed);
    }

    std::wstring wstrTo(size_needed, 0);
    MultiByteToWideChar(CP_UTF8, 0, &str[0], (int) str.size(), &wstrTo[0], size_needed);
    return wstrTo;
#else
    auto ustr = miniutf::to_utf16(str);
    return WSTRING(reinterpret_cast<const WCHAR*>(ustr.c_str()));
#endif
}

WSTRING ToWSTRING(const uint64_t i)
{
    return WSTRING(reinterpret_cast<const WCHAR*>(std::to_wstring(i).c_str()));
}

constexpr char HexMap[] = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'};

WSTRING HexStr(const void* dataPtr, int len)
{
    const unsigned char* data = (unsigned char*) dataPtr;
    WSTRING s(len * 2, ' ');
    for (int i = 0; i < len; ++i)
    {
        s[2 * i] = HexMap[(data[i] & 0xF0) >> 4];
        s[2 * i + 1] = HexMap[data[i] & 0x0F];
    }
    return s;
}

WSTRING Trim(const WSTRING& str)
{
    if (str.length() == 0)
    {
        return WStr("");
    }

    const WSTRING WhiteSpaceChars = WStr(" \f\n\r\t\v");

    WSTRING trimmed = str;

    auto lpos = trimmed.find_first_not_of(WhiteSpaceChars);
    if (lpos != WSTRING::npos && lpos > 0)
    {
        trimmed = trimmed.substr(lpos);
    }

    auto rpos = trimmed.find_last_not_of(WhiteSpaceChars);
    if (rpos != WSTRING::npos)
    {
        trimmed = trimmed.substr(0, rpos + 1);
    }

    return trimmed;
}

std::string Trim(const std::string& str)
{
    if (str.length() == 0)
    {
        return "";
    }

    const char* WhiteSpaceChars = " \f\n\r\t\v";

    std::string trimmed = str;

    auto lpos = trimmed.find_first_not_of(WhiteSpaceChars);
    if (lpos != std::string::npos && lpos > 0)
    {
        trimmed = trimmed.substr(lpos);
    }

    auto rpos = trimmed.find_last_not_of(WhiteSpaceChars);
    if (rpos != std::string::npos)
    {
        trimmed = trimmed.substr(0, rpos + 1);
    }

    return trimmed;
}

template <typename Out>
void Split(const WSTRING& s, wchar_t delim, Out result)
{
    size_t lpos = 0;
    for (size_t i = 0; i < s.length(); i++)
    {
        if (s[i] == delim)
        {
            *(result++) = s.substr(lpos, (i - lpos));
            lpos = i + 1;
        }
    }
    *(result++) = s.substr(lpos);
}

std::vector<WSTRING> Split(const WSTRING& s, wchar_t delim)
{
    std::vector<WSTRING> elems;
    Split(s, delim, std::back_inserter(elems));
    return elems;
}

template <typename Out>
void Split(const std::string& s, char delim, Out result)
{
    size_t lpos = 0;
    for (size_t i = 0; i < s.length(); i++)
    {
        if (s[i] == delim)
        {
            *(result++) = s.substr(lpos, (i - lpos));
            lpos = i + 1;
        }
    }
    *(result++) = s.substr(lpos);
}

std::vector<std::string> Split(const std::string& s, char delim)
{
    std::vector<std::string> elems;
    Split(s, delim, std::back_inserter(elems));
    return elems;
}