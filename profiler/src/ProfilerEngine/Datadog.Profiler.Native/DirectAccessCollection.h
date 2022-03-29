// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

template <class TElement>
class DirectAccessCollection
{
public:
    explicit DirectAccessCollection(std::uint32_t count);
    ~DirectAccessCollection();
    DirectAccessCollection(DirectAccessCollection const&) = delete;
    DirectAccessCollection& operator=(DirectAccessCollection const&) = delete;

    bool TryGet(std::uint32_t index, TElement** ppValue);
    bool TrySet(std::uint32_t index, const TElement& value);
    std::uint32_t Count();

private:
    TElement* _data;
    std::uint32_t _count;
};

template <class TElement>
inline DirectAccessCollection<TElement>::DirectAccessCollection(std::uint32_t count)
{
    _count = count;
    _data = new TElement[_count];
}

template <class TElement>
inline DirectAccessCollection<TElement>::~DirectAccessCollection()
{
    delete[] _data;
    _data = nullptr;
}

template <class TElement>
inline bool DirectAccessCollection<TElement>::TryGet(std::uint32_t index, TElement** ppValue)
{
    if (index >= _count)
    {
        return false;
    }

    *ppValue = (_data + index);
    return true;
}

template <class TElement>
inline bool DirectAccessCollection<TElement>::TrySet(std::uint32_t index, const TElement& value)
{
    if (index >= _count)
    {
        return false;
    }

    _data[index] = value;
    return true;
}

template <class TElement>
inline std::uint32_t DirectAccessCollection<TElement>::Count()
{
    return _count;
}
