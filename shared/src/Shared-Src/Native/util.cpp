#include "util.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string> //NOLINT
#include <vector>

namespace shared
{
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

    WSTRING Trim(const WSTRING& str)
    {
        if (str.length() == 0)
        {
            return WStr("");
        }

        WSTRING trimmed = str;

        auto lpos = trimmed.find_first_not_of(WStr(" \t"));
        if (lpos != WSTRING::npos && lpos > 0)
        {
            trimmed = trimmed.substr(lpos);
        }

        auto rpos = trimmed.find_last_not_of(WStr(" \t"));
        if (rpos != WSTRING::npos)
        {
            trimmed = trimmed.substr(0, rpos + 1);
        }

        return trimmed;
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

}  // namespace trace
