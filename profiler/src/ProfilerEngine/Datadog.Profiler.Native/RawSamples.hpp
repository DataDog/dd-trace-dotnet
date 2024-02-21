
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>
#include <memory>

#include "IConfiguration.h"

template <class Ty>
class ListBasedRawSamples;

template <class TRawSample, class TRawSamplesImpl>
struct RawSamplesTraits
{
};

template <class TRawSample>
struct RawSamplesTraits<TRawSample, ListBasedRawSamples<TRawSample>>
{
    using const_iterator = typename std::list<TRawSample>::const_iterator;
};

template <class TRawSample, class Impl = ListBasedRawSamples<TRawSample>>
class RawSamples
{
public:
    using const_iterator = typename RawSamplesTraits<TRawSample, Impl>::const_iterator;

    std::unique_ptr<RawSamples<TRawSample>> Move()
    {
        return Internal().MoveImpl();
    }

    void Add(TRawSample&& sample)
    {
        Internal().AddImpl(std::move(sample));
    }

    auto cbegin() const
    {
        return Internal().cbeginImpl();
    }

    auto cend() const
    {
        return Internal().cendImpl();
    }

    std::size_t size() const
    {
        return Internal().sizeImpl();
    }

    virtual ~RawSamples() = default;
protected:
    RawSamples() = default;

    RawSamples(RawSamples&& other) = default;
    RawSamples& operator=(RawSamples&& other) = default;

    RawSamples(RawSamples const&) = delete;
    RawSamples& operator=(RawSamples const& other) = delete;

private:
    Impl& Internal() { return *static_cast<Impl*>(this); }
    Impl const& Internal() const { return *static_cast<Impl const*>(this); }
};

template <class TRawSample>
class ListBasedRawSamples final : public RawSamples<TRawSample, ListBasedRawSamples<TRawSample>>
{
public:
    ListBasedRawSamples() = default;
    ~ListBasedRawSamples() override = default;
private:

    ListBasedRawSamples(std::list<TRawSample> samples) :
        _samples{std::move(samples)}
    {
    }

    friend RawSamples<TRawSample, ListBasedRawSamples<TRawSample>>;
    friend std::unique_ptr<ListBasedRawSamples<TRawSample>> std::make_unique<ListBasedRawSamples<TRawSample>>(std::list<TRawSample>&&);

    void AddImpl(TRawSample&& sample)
    {
        std::lock_guard<std::mutex> lock(_lock);
        _samples.push_back(std::forward<TRawSample>(sample));
    }

    std::unique_ptr<RawSamples<TRawSample, ListBasedRawSamples<TRawSample>>> MoveImpl()
    {
        std::lock_guard<std::mutex> lock(_lock);

        std::list<TRawSample> result;
        _samples.swap(result);

        return std::make_unique<ListBasedRawSamples<TRawSample>>(std::move(result));
    }
    
    auto cbeginImpl() const
    {
        return _samples.cbegin();
    }

    auto cendImpl() const
    {
        return _samples.cend();
    }

    std::size_t sizeImpl() const
    {
        return _samples.size();
    }

    std::mutex _lock;
    std::list<TRawSample> _samples;
};

class RawSamplesFactory
{
public:
    RawSamplesFactory(IConfiguration* configuration) : _configuration{configuration}
    {}

    template <class TRawSample>
    static std::unique_ptr<RawSamples<TRawSample>> Create()
    {
        return std::make_unique<ListBasedRawSamples<TRawSample>>();
    }

private:
    IConfiguration* _configuration;
};