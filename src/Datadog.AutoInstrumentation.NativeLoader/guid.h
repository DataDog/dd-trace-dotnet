//-------------------------------------------------------------------------------------------------------
// constexpr GUID parsing
// Written by Alexander Bessonov
//
// Licensed under the MIT license.
// https://gist.github.com/AlexBAV/b58e92d7632bae5d6f5947be455f796f
//-------------------------------------------------------------------------------------------------------

#pragma once
#include <cassert>
#include <cstdint>
#include <stdexcept>
#include <string>

#if !defined(GUID_DEFINED)
#define GUID_DEFINED
struct GUID
{
    uint32_t Data1;
    uint16_t Data2;
    uint16_t Data3;
    uint8_t Data4[8];
};
#endif

namespace guid_parse
{
namespace details
{
    constexpr const size_t short_guid_form_length = 36; // XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX
    constexpr const size_t long_guid_form_length = 38;  // {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}

    //
    constexpr int parse_hex_digit(const char c)
    {
        using namespace std::string_literals;
        if ('0' <= c && c <= '9')
            return c - '0';
        else if ('a' <= c && c <= 'f')
            return 10 + c - 'a';
        else if ('A' <= c && c <= 'F')
            return 10 + c - 'A';
        else
            throw std::domain_error{"invalid character in GUID"s};
    }

    template <class T>
    constexpr T parse_hex(const char* ptr)
    {
        constexpr size_t digits = sizeof(T) * 2;
        T result{};
        for (size_t i = 0; i < digits; ++i) result |= parse_hex_digit(ptr[i]) << (4 * (digits - i - 1));
        return result;
    }

    constexpr GUID make_guid_helper(const char* begin)
    {
        GUID result{};
        result.Data1 = parse_hex<uint32_t>(begin);
        begin += 8 + 1;
        result.Data2 = parse_hex<uint16_t>(begin);
        begin += 4 + 1;
        result.Data3 = parse_hex<uint16_t>(begin);
        begin += 4 + 1;
        result.Data4[0] = parse_hex<uint8_t>(begin);
        begin += 2;
        result.Data4[1] = parse_hex<uint8_t>(begin);
        begin += 2 + 1;
        for (size_t i = 0; i < 6; ++i) result.Data4[i + 2] = parse_hex<uint8_t>(begin + i * 2);
        return result;
    }

    template <size_t N>
    constexpr GUID make_guid(const char (&str)[N])
    {
        using namespace std::string_literals;
        static_assert(N == (long_guid_form_length + 1) || N == (short_guid_form_length + 1),
                      "String GUID of the form {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX} or "
                      "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX is expected");

        if constexpr (N == (long_guid_form_length + 1))
        {
            if (str[0] != '{' || str[long_guid_form_length - 1] != '}')
                throw std::domain_error{"Missing opening or closing brace"s};
        }

        return make_guid_helper(str + (N == (long_guid_form_length + 1) ? 1 : 0));
    }

    GUID make_guid(const std::string str)
    {
        size_t currentLength = str.length();

        if (currentLength == long_guid_form_length || currentLength == short_guid_form_length)
        {
            if (currentLength == long_guid_form_length)
            {
                if (str[0] != '{' || str[long_guid_form_length - 1] != '}')
                    throw std::domain_error{"Missing opening or closing brace"};
            }

            return make_guid_helper(str.c_str() + (currentLength == long_guid_form_length ? 1 : 0));
        }

        throw std::domain_error{"String GUID of the form {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX} or "
                                "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX is expected"};
    }
} // namespace details
using details::make_guid;

namespace literals
{
    constexpr GUID operator"" _guid(const char* str, size_t N)
    {
        using namespace std::string_literals;
        using namespace details;

        if (!(N == long_guid_form_length || N == short_guid_form_length))
            throw std::domain_error{
                "String GUID of the form {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX} or XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX is expected"s};
        if (N == long_guid_form_length && (str[0] != '{' || str[long_guid_form_length - 1] != '}'))
            throw std::domain_error{"Missing opening or closing brace"s};

        return make_guid_helper(str + (N == long_guid_form_length ? 1 : 0));
    }
} // namespace literals
} // namespace guid_parse