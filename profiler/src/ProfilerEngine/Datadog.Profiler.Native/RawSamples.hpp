
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>
#include <mutex>

// forward declarations
template <class TRawSample>
class RawSampleHolder;

template <class TRawSample>
class RawSamples
{
public:
    using const_iterator = typename std::list<TRawSample>::const_iterator;

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

    RawSamples<TRawSample> FetchRawSamples()
    {
        std::lock_guard<std::mutex> lock(_lock);

        std::list<TRawSample> result;
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

    RawSampleHolder<TRawSample> CreateRawSample()
    {
        return {*this};
    }

private:
    RawSamples(std::list<TRawSample> samples) :
        _samples{std::move(samples)}
    {
    }

    std::mutex _lock;
    std::list<TRawSample> _samples;
};


template <class TRawSample>
class RawSampleHolder
{
public:
    RawSampleHolder(RawSamples<TRawSample>& container) :
        _container{container},
        _sample{}
    {
    }

    ~RawSampleHolder()
    {
        _container.Add(std::move(_sample));
    }

    TRawSample* operator->()
    {
        return &_sample;
    }

    operator TRawSample&()
    {
        return _sample;
    }

private:
    RawSamples<TRawSample>& _container;
    TRawSample _sample;
};