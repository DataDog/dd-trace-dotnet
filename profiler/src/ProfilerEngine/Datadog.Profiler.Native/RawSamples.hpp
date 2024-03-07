
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "LinkedList.hpp"

template <class TRawSample>
class RawSamples
{
public:
    using iterator = typename LinkedList<TRawSample>::iterator;

    RawSamples() = default;

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

        LinkedList<TRawSample> result;
        _samples.swap(result);

        return RawSamples(std::move(result));
    }

    void Add(TRawSample&& sample)
    {
        std::lock_guard<std::mutex> lock(_lock);
        _samples.append(std::forward<TRawSample>(sample));
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
        return _samples.size();
    }

private:
    RawSamples(LinkedList<TRawSample> samples) :
        _samples{std::move(samples)}
    {
    }

    std::mutex _lock;
    LinkedList<TRawSample> _samples;
};