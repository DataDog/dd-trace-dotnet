
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>

#include "allocators.h"

template <class TRawSample>
class RawSamples
{
public:
    using const_iterator = typename std::list<TRawSample>::const_iterator;

    RawSamples() :
        _allocator{allocators::get_default_sample_allocator<TRawSample>()},
        _samples(_allocator)
    {

    }

    RawSamples(RawSamples&& other)
    {
        *this = std::move(other);
    };

    RawSamples& operator=(RawSamples&& other)
    {
        _samples = std::move(other._samples);
        return *this;
    }

    RawSamples(RawSamples const&) = delete;
    RawSamples& operator=(RawSamples const& other) = delete;

    RawSamples<TRawSample> Move()
    {
        std::lock_guard<std::mutex> lock(_lock);

        std::pmr::list<TRawSample> result{allocators::get_default_sample_allocator<TRawSample>()};
        _samples.swap(result);

        return RawSamples(std::move(result));
    }

    void Add(TRawSample&& sample)
    {
        std::lock_guard<std::mutex> lock(_lock);
        _samples.push_back(std::forward<TRawSample>(sample));
    }

    auto cbegin() const
    {
        return _samples.cbegin();
    }

    auto cend() const
    {
        return _samples.cend();
    }

    std::size_t size() const
    {
        return _samples.size();
    }

private:
    RawSamples(std::pmr::list<TRawSample> samples) :
        _samples{std::move(samples)}
    {
    }

    std::mutex _lock;
    pmr::polymorphic_allocator<TRawSample> _allocator;
    std::pmr::list<TRawSample> _samples;
};