// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Callstack.h"
#include "DiscardMetrics.h"
#include "MetricsRegistry.h"
#include "RingBuffer.h"
#include "ProviderBase.h"
#include "ServiceBase.h"
#include "SamplesEnumerator.h"
#include "SampleValueTypeProvider.h"
#include "RawSampleTransformer.h"

#include <memory>

template <typename TRawSample>
class RawSampleCollectorBase : public ServiceBase,
                                   public ProviderBase
{
public:
    static constexpr std::size_t SampleSize = sizeof(TRawSample) + Callstack::MaxSize;

    class SampleHolder
    {
    public:
        explicit SampleHolder(RingBuffer* ringBuffer, DiscardMetrics* failedReservationMetric)
            : _rbw{ringBuffer->GetWriter()}, _sample{nullptr}, _discard{false}
        {
            bool timedOut = false;
            _buffer = _rbw.Reserve(&timedOut);
            if (!timedOut && !_buffer.empty())
            {
                _sample = new(_buffer.data()) TRawSample();
                _sample->Stack = Callstack(shared::span<std::uintptr_t>(
                    reinterpret_cast<std::uintptr_t*>(_sample + 1), Callstack::MaxFrames));
            }

            if (timedOut)
            {
                failedReservationMetric->Incr<DiscardReason::TimedOut>();
            }
            if (_buffer.empty())
            {
                failedReservationMetric->Incr<DiscardReason::UnsufficientSpace>();
            }
        }
        ~SampleHolder()
        {
            if (_buffer.empty())
            {
                return;
            }

            _sample = nullptr;
            if (_discard)
            {
                _rbw.Discard(_buffer);
            }
            else
            {
                _rbw.Commit(_buffer);
            }
        }

        SampleHolder(SampleHolder const&) = delete;
        SampleHolder& operator=(SampleHolder const&) = delete;
        SampleHolder(SampleHolder&&) = delete;
        SampleHolder& operator=(SampleHolder&&) = delete;

        operator bool() const
        {
            return _sample != nullptr;
        }

        TRawSample* operator->()
        {
            return _sample;
        }

        void Discard()
        {
            _discard = true;
        }

    private:
        Buffer _buffer;
        RingBuffer::Writer _rbw;
        TRawSample* _sample;
        bool _discard;
    };

public:
    RawSampleCollectorBase(
        const char* name,
        std::vector<SampleValueTypeProvider::Offset> valueOffsets,
        RawSampleTransformer* rawSampleTransformer,
        RingBuffer* ringBuffer,
        MetricsRegistry& metricsRegistry) :
        ProviderBase(name),
        _valueOffsets{std::move(valueOffsets)},
        _rawSampleTransformer{rawSampleTransformer},
        _collectedSamples{ringBuffer},
        _failedReservationMetric{metricsRegistry.GetOrRegister<DiscardMetrics>("dotnet_raw_sample_failed_allocation")}
    {
    }

    ~RawSampleCollectorBase() override = default;

    SampleHolder GetRawSample()
    {
        return SampleHolder(_collectedSamples, _failedReservationMetric.get());
    }
    
    const char* GetName() override
    {
        return _name.c_str();
    }

    std::unique_ptr<SamplesEnumerator> GetSamples() override
    {
        return std::make_unique<SamplesEnumeratorImpl>(std::move(_collectedSamples->GetReader()), _rawSampleTransformer, _valueOffsets);
    }

private:
    class SamplesEnumeratorImpl : public SamplesEnumerator
    {
    public:
        SamplesEnumeratorImpl(RingBuffer::Reader&& reader,
                              RawSampleTransformer* rawSampleTransformer,
                              std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) :
            _reader{std::move(reader)},
            _rawSampleTransformer{rawSampleTransformer},
            _valueOffsets{valueOffsets}
        {
        }

        SamplesEnumeratorImpl(SamplesEnumeratorImpl const&) = delete;
        SamplesEnumeratorImpl& operator=(SamplesEnumeratorImpl const&) = delete;
        SamplesEnumeratorImpl(SamplesEnumeratorImpl&&) = delete;
        SamplesEnumeratorImpl& operator=(SamplesEnumeratorImpl&&) = delete;
        ~SamplesEnumeratorImpl() override = default;

        // Inherited via SamplesEnumerator
        std::size_t size() const override
        {
            return _reader.AvailableSamples();
        }

        bool MoveNext(std::shared_ptr<Sample>& sample) override
        {
            ConstBuffer buffer = _reader.GetNext();
            if (buffer.empty())
            {
                return false;
            }

            auto* rawSample = const_cast<TRawSample*>(
                reinterpret_cast<TRawSample const*>(buffer.data()));

            _rawSampleTransformer->Transform(*rawSample, sample, _valueOffsets);
            std::destroy_at(rawSample);
            return true;
        }

    private:
        RingBuffer::Reader _reader;
        RawSampleTransformer* _rawSampleTransformer;
        std::vector<SampleValueTypeProvider::Offset> const& _valueOffsets;
    };

    bool StartImpl() override
    {
        return true;
    }

    bool StopImpl() override
    {
        return true;
    }

    std::vector<SampleValueTypeProvider::Offset> _valueOffsets;
    RawSampleTransformer* _rawSampleTransformer;
    RingBuffer* _collectedSamples;
    std::shared_ptr<DiscardMetrics> _failedReservationMetric;
};