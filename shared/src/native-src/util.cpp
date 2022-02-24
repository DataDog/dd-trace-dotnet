#include "util.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string> //NOLINT
#include <vector>

namespace shared
{
    template <typename In, typename Out>
    void Split(const In& s, typename In::value_type delim, Out result)
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

    std::vector<std::string> Split(const std::string& s, char delim)
    {
        std::vector<std::string> elems;
        Split(s, delim, std::back_inserter(elems));
        return elems;
    }

    template <typename T>
    T Trim(const T& str, typename T::const_pointer whiteSpaceChars)
    {
        if (str.length() == 0)
        {
            return {};
        }

        T trimmed = str;

        auto lpos = trimmed.find_first_not_of(whiteSpaceChars);
        if (lpos != T::npos && lpos > 0)
        {
            trimmed = trimmed.substr(lpos);
        }

        auto rpos = trimmed.find_last_not_of(whiteSpaceChars);
        if (rpos != T::npos)
        {
            trimmed = trimmed.substr(0, rpos + 1);
        }

        return trimmed;
    }

    WSTRING Trim(const WSTRING& str)
    {
        return Trim(str, WStr(" \t"));
    }

    std::string Trim(const std::string& str)
    {
        return Trim(str, " \f\n\r\t\v");
    }

    bool TryParseBooleanEnvironmentValue(const WSTRING& valueToParse, bool& parsedValue)
    {
        WSTRING trimmedValueToParse = Trim(valueToParse);

        // In the future we should convert trimmedValueToParse to lower case in a portable manner and simplify the IFs below.
        // Being pragmatic for now.

        if (trimmedValueToParse == WStr("false")
                || trimmedValueToParse == WStr("False")
                || trimmedValueToParse == WStr("FALSE")
                || trimmedValueToParse == WStr("no")
                || trimmedValueToParse == WStr("No")
                || trimmedValueToParse == WStr("NO")
                || trimmedValueToParse == WStr("f")
                || trimmedValueToParse == WStr("F")
                || trimmedValueToParse == WStr("N")
                || trimmedValueToParse == WStr("n")
                || trimmedValueToParse == WStr("0"))
        {
            parsedValue = false;
            return true;
        }

        if (trimmedValueToParse == WStr("true")
                || trimmedValueToParse == WStr("True")
                || trimmedValueToParse == WStr("TRUE")
                || trimmedValueToParse == WStr("yes")
                || trimmedValueToParse == WStr("Yes")
                || trimmedValueToParse == WStr("YES")
                || trimmedValueToParse == WStr("t")
                || trimmedValueToParse == WStr("T")
                || trimmedValueToParse == WStr("Y")
                || trimmedValueToParse == WStr("y")
                || trimmedValueToParse == WStr("1"))
        {
            parsedValue = true;
            return true;
        }

        return false;
    }


    WSTRING GetEnvironmentValue(const WSTRING& name)
    {
#ifdef _WIN32
        const size_t max_buf_size = 4096;
        WSTRING buf(max_buf_size, 0);
        auto len = GetEnvironmentVariable((LPWSTR)name.data(), (LPWSTR)buf.data(), (DWORD)(buf.size()));
        return Trim(buf.substr(0, len));
#else
        auto cstr = std::getenv(ToString(name).c_str());
        if (cstr == nullptr)
        {
            return WStr("");
        }
        std::string str(cstr);
        auto wstr = ToWSTRING(str);
        return Trim(wstr);
#endif
    }

    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name, const wchar_t delim)
    {
        std::vector<WSTRING> values;
        for (auto s : Split(GetEnvironmentValue(name), delim))
        {
            s = Trim(s);
            if (!s.empty())
            {
                values.push_back(s);
            }
        }
        return values;
    }

    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name)
    {
        return GetEnvironmentValues(name, L';');
    }

    WSTRING WHexStr(const void* pData, int len)
    {
        const unsigned char* data = (unsigned char*)pData;
        WSTRING s(len * 2, ' ');
        for (int i = 0; i < len; ++i)
        {
            s[2 * i] = HexMap[(data[i] & 0xF0) >> 4];
            s[2 * i + 1] = HexMap[data[i] & 0x0F];
        }
        return s;
    }

    shared::WSTRING HexStr(const void* dataPtr, int len)
    {
        const unsigned char* data = (unsigned char*) dataPtr;
        shared::WSTRING s(len * 2, ' ');
        for (int i = 0; i < len; ++i)
        {
            s[2 * i] = HexMap[(data[i] & 0xF0) >> 4];
            s[2 * i + 1] = HexMap[data[i] & 0x0F];
        }
        return s;
    }

    shared::WSTRING TokenStr(const mdToken* token)
    {
        const unsigned char* data = (unsigned char*) token;
        int len = sizeof(mdToken);
        shared::WSTRING s(len * 2, ' ');
        for (int i = 0; i < len; i++)
        {
            s[(2 * (len - i)) - 2] = HexMap[(data[i] & 0xF0) >> 4];
            s[(2 * (len - i)) - 1] = HexMap[data[i] & 0x0F];
        }
        return s;
    }

    bool SetEnvironmentValue(const ::shared::WSTRING& name, const ::shared::WSTRING& value)
    {
        /*
        Environment variables set with SetEnvironmentVariable() are not seen by
        getenv() (although GetEnvironmentVariable() sees changes done by
        putenv()), and since SetEnvironmentVariable() is preferable to putenv()
        because the former is thread-safe we use different apis for Windows implementation.
        */

#ifdef _WIN32
        return SetEnvironmentVariable(::shared::Trim(name).c_str(), value.c_str());
#else
        return setenv(::shared::ToString(name).c_str(), ::shared::ToString(value).c_str(), 1) == 1;
#endif
    }

}  // namespace trace
