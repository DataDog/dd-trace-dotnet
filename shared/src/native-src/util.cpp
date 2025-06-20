#include "util.h"

#include <cwctype>
#include <iterator>
#include <random>
#include <sstream>
#include <string> //NOLINT
#include <vector>
#if LINUX
#include <dlfcn.h>
#endif

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

    bool IsEmptyOrWhitespace(const std::string& s)
    {
        const char* WhiteSpaceChars = " \f\n\r\t\v";
        return s.find_first_not_of(WhiteSpaceChars) == WSTRING::npos;
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
        return Trim(str, WStr(" \f\n\r\t\v"));
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

    bool EnvironmentExist(const WSTRING& name)
    {
#ifdef _WIN32
        auto len = ::GetEnvironmentVariable((LPWSTR)name.data(), (LPWSTR)nullptr, 0);
        if (len > 0)
        {
            return true;
        }

        return (::GetLastError() != ERROR_ENVVAR_NOT_FOUND);
#else
        auto cstr = std::getenv(ToString(name).c_str());
        return (cstr != nullptr);
#endif
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
        return setenv(::shared::ToString(name).c_str(), ::shared::ToString(value).c_str(), 1) == 0;
#endif
    }

    bool UnsetEnvironmentValue(const ::shared::WSTRING& name)
    {
#ifdef _WIN32
        return ::SetEnvironmentVariable(name.c_str(), nullptr);
#else
        return unsetenv(::shared::ToString(name).c_str()) == 0;
#endif
    }

    // copied from https://stackoverflow.com/a/60198074
    // We replace std::mt19937 by std::mt19937_64 so we can generate 64bits numbers instead of 32bits
    std::string GenerateUuidV4()
    {
        static std::random_device rd;
        static std::mt19937_64 gen(rd());
        static std::uniform_int_distribution<> dis(0, 15);
        static std::uniform_int_distribution<> dis2(8, 11);

        std::stringstream ss;
        ss << std::hex;
        for (auto i = 0; i < 8; i++)
        {
            ss << dis(gen);
        }
        ss << "-";
        for (auto i = 0; i < 4; i++)
        {
            ss << dis(gen);
        }
        ss << "-4"; // according to the RFC, '4' is the 4 version
        for (auto i = 0; i < 3; i++)
        {
            ss << dis(gen);
        }
        ss << "-";
        ss << dis2(gen);
        for (auto i = 0; i < 3; i++)
        {
            ss << dis(gen);
        }
        ss << "-";
        for (auto i = 0; i < 12; i++)
        {
            ss << dis(gen);
        }
        return ss.str();
    }

    std::string GenerateRuntimeId()
    {
#ifdef WIN32
        UUID uuid;
        UuidCreate(&uuid);

        unsigned char* str;
        UuidToStringA(&uuid, &str);

        std::string s((char*) str);

        RpcStringFreeA(&str);
        return s;
#else
        return GenerateUuidV4();
#endif
    }

	bool WStringStartWithCaseInsensitive(const WSTRING& longer, const WSTRING& shorter)
    {
        if (shorter.length() > longer.length()) return false;

        return std::mismatch(std::cbegin(shorter), std::cend(shorter), std::cbegin(longer),
                             [&](const WCHAR a, const WCHAR b) { return std::tolower(a) == std::tolower(b); })
                   .first == std::cend(shorter);
    }


#if LINUX
    std::tuple<bool, WSTRING> HasBuggyDlclose()
    {
        // In certain versions of glibc, there is a TLS-reuse bug that can cause crashes when unloading shared libraries.
        // The bug was introduced in 2.34, fixed in 2.36 on x86-64, and fixed in 2.37 on aarch64.
        // See https://sourceware.org/git/gitweb.cgi?p=glibc.git;h=3921c5b40f293c57cb326f58713c924b0662ef59
        // Explanation in Fedora where we spotted it: https://bugzilla.redhat.com/show_bug.cgi?id=2251557
        //
        // 2.34 shipped with a regression: after a dlclose() of a library that carried dynamic-TLS, the loader could reuse
        // the same “module-ID” for a different library without first clearing the associated DTV (Dynamic Thread Vector)
        // entry. The next time any code accessed that TLS slot it could read or write an unmapped address → SIGSEGV.
        //
        // This manifested as a crash in the WAF when we called `ddwaf_context_info` on arm64. It explicitly happens
        // on arm64 when we unload the continuous profiler (because it's not supported).
        //
        // It manifests in this scenario, because ddwaf_context_init starts like this:
        //   +128  bl   __tls_get_addr     ; ask glibc for the TLS slot for libddwaf
        //   +136  mrs  x11, TPIDR_EL0     ; TLS base for this thread
        //   +140  ldrb w9, [x11, x0]      ; <–– boom if x0 points to a stale DTV entry
        //
        // When we unload the continuous profiler and call dlcose, it causes the loader to hand out a recycled module-ID
        // to libddwaf. When ddwaf_context_init tries to access TLS in the `ldrb` instruction, `x11 + x0` is outside
        // every mapped version, and so crashes.
        //
        // Note that although calling dlclose with the continuous profiler may trigger the issue (the actual crash is flaky
        // depending on load/unload timing and address layout), unloading _any_ library that is is built with
        // `__thread`/`thread_local` data could trigger the crash. To minimize the risk of hitting this issue,

        // Cache the value statically to avoid repeated checks
        static std::tuple<bool, WSTRING> result = []() {
            // Need to check whether we can close libraries or not
            // But we compile for both musl and glibc, so need to manually
            // try to load the glibc function gnu_get_libc_version
            if (::shared::IsRunningOnAlpine()) {
                // definitely running on alpine
                return std::make_tuple(false, EmptyWStr);
            }

            void* handle = dlopen("libc.so.6", RTLD_LAZY);
            if (!handle) {
                // Likely not glibc (e.g. non-alpine musl or other libc)
                return std::make_tuple(false, EmptyWStr);
            }

            using gnu_get_libc_version_fn = const char* (*)();
            auto func = (gnu_get_libc_version_fn)dlsym(handle, "gnu_get_libc_version");
            if (!func) {
                // We do have glibc, but the function is not available...
                // This shouldn't happen given it's available since 2.1 and we require 2.17
                // so overall, a bit weird... Don't close the handle, just in case, and
                // treat it as faulty
                // dlclose(handle);
                return std::make_tuple(true, ::shared::ToWSTRING("unknown"));
            }

            // Check if it's one of the buggy versions
            const auto version = std::string(func());
#if ARM64
            const auto is_buggy = (version == "2.34" || version == "2.35" || version == "2.36");
#else
            const auto is_buggy = (version == "2.34" || version == "2.35");
#endif

            if (!is_buggy) {
                // Not buggy, so we can close the handle
                dlclose(handle);
                return std::make_tuple(false, ::shared::ToWSTRING(version));
            }

            // buggy, so we can't close the handle
            return std::make_tuple(true, ::shared::ToWSTRING(version));
        }();

        return result;
    }
#endif

}  // namespace shared
