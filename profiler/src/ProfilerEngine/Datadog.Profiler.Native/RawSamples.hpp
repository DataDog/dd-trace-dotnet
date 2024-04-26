
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "LinkedList.hpp"

#include "shared/src/native-src/dd_memory_resource.hpp"

template <class TRawSample>
class RawSamples
{
public:
    using iterator = typename LinkedList<TRawSample>::iterator;

    RawSamples(shared::pmr::memory_resource* memoryResource) :
        _memoryResource{memoryResource},
        _samples{memoryResource}
    {
    }

    RawSamples(RawSamples&& other) :
        RawSamples(shared::pmr::get_default_resource())
    {
        *this = std::move(other);
    };

    RawSamples& operator=(RawSamples&& other)
    {
        _samples = std::move(other._samples);
        std::swap(_memoryResource, other._memoryResource);

        return *this;
    }

    RawSamples(RawSamples const&) = delete;
    RawSamples& operator=(RawSamples const& other) = delete;

    RawSamples<TRawSample> Move()
    {
        std::lock_guard<std::mutex> lock(_lock);

        LinkedList<TRawSample> result{_memoryResource};
        _samples.Swap(result);

        return RawSamples(std::move(result), _memoryResource);
    }

    void Add(TRawSample&& sample)
    {
        std::lock_guard<std::mutex> lock(_lock);
        _samples.Append(std::forward<TRawSample>(sample));
    }

    auto begin()
    {
        return _samples.begin();
    }

    auto end()
    {
        return _samples.end();
    }

    std::size_t size() const
    {
        return _samples.Size();
    }

private:
    RawSamples(LinkedList<TRawSample> samples, shared::pmr::memory_resource* memoryResource) :
        _samples{std::move(samples)},
        _memoryResource{memoryResource}
    {
    }

    std::mutex _lock;
    LinkedList<TRawSample> _samples;
    shared::pmr::memory_resource* _memoryResource;
};
