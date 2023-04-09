// Copyright (c) 2019 Aaron R Robinson

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#ifndef _PPDB_INC_PLATFORM_HPP_
#define _PPDB_INC_PLATFORM_HPP_

#include <cstdint>
#include <cassert>

#ifndef GUID_DEFINED
struct GUID
{
    uint32_t Data1;
    uint16_t Data2;
    uint16_t Data3;
    uint8_t  Data4[8];
};
#endif

static_assert(sizeof(GUID) == 16, "GUID should be 16 bytes");

#ifndef MDTOKEN_DEFINED
using mdToken = uint32_t;
const mdToken mdTokenNil = mdToken{ 0 };
#endif

namespace plat
{
    template <typename T>
    class data_view
    {
    public:
        using value_type = T;
        using pointer = T *;
        using const_pointer = const T *;
        using size_type = size_t;
        using iterator = const_pointer;
        using const_iterator = const_pointer;

        constexpr data_view() noexcept
            : _data{ nullptr }
            , _size{ 0 }
        {
        }

        constexpr data_view(size_type size, const_pointer arr) noexcept
            : _data{ arr }
            , _size{ size }
        {
        }

        template <typename U>
        constexpr data_view(const data_view<U> &other) noexcept
            : _data{ reinterpret_cast<const_pointer>(other.data()) }
            , _size{ other.size() / sizeof(value_type) }
        {
        }

        template <typename U, size_t US>
        constexpr data_view(const U(&arr)[US]) noexcept
            : _data{ reinterpret_cast<const_pointer>(arr) }
            , _size{ US }
        {
        }

        data_view(const data_view &) = default;
        data_view &operator=(const data_view &) = default;

        data_view(data_view &&) = default;
        data_view &operator=(data_view &&) = default;

        constexpr size_type size() const noexcept
        {
            return (_size);
        }

        constexpr bool empty() const noexcept
        {
            return (size() == 0);
        }

        constexpr const value_type& front() const
        {
            assert(_data != nullptr);
            return (_data[0]);
        }

        constexpr const value_type& back() const
        {
            assert(size() > 0);
            return (_data[size() - 1]);
        }

        constexpr const_pointer data() const noexcept
        {
            return (_data);
        }

        constexpr const_iterator cbegin() const noexcept
        {
            return (_data);
        }

        constexpr const_iterator cend() const noexcept
        {
            return (_data + _size);
        }

        constexpr iterator begin() const noexcept
        {
            return (cbegin());
        }

        constexpr iterator end() const noexcept
        {
            return (cend());
        }

        const value_type & operator[](size_t i) const
        {
            assert(_data != nullptr && i < size());
            return (_data[i]);
        }

    private:
        const_pointer _data;
        size_type _size;
    };
}

#endif // _PPDB_INC_PLATFORM_HPP_
