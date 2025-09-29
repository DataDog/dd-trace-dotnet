// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IAppDomainStore.h"
#include "ICollector.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "Log.h"
#include "OpSysTools.h"
#include "ProviderBase.h"
#include "RawSample.h"
#include "RawSampleTransformer.h"
#include "RawSamples.hpp"
#include "SamplesEnumerator.h"
#include "SampleValueTypeProvider.h"
#include "ServiceBase.h"

#include "shared/src/native-src/dd_memory_resource.hpp"
#include "shared/src/native-src/string.h"

#include <list>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

// forward declarations
class IConfiguration;
class IFrameStore;
class IAppDomainStore;

using namespace std::chrono_literals;

// Base class used for storing raw samples that are transformed into exportable Sample
// every hundreds of milliseconds. The transformation code is setting :
//  - common labels (process id, thread id, appdomain, span ids) into Sample instances
//  - symbolized call stack (TODO: how to define a fake call stack? should we support
//    "hardcoded" fake IPs in the symbol store (0 = "Heap Profiler")?)
//
// Each profiler has to implement an inherited class responsible for setting its
// specific labels (such as exception name or exception message) if any but more important,
// to set its value(s) like wall time duration or cpu time duration.
//
template <class TRawSample> // TRawSample is supposed to inherit from RawSample
class CollectorBase
    : public ServiceBase,
      public ICollector<TRawSample>, // allows profilers to add TRawSample instances
      public ProviderBase            // returns Samples to the aggregator
{
public:
    CollectorBase<TRawSample>(
        const char* name,
        std::vector<SampleValueTypeProvider::Offset> valueOffsets,
        RawSampleTransformer* rawSampleTransformer,
        shared::pmr::memory_resource* memoryResource)
        :
        ProviderBase(name),
        _valueOffsets{std::move(valueOffsets)},
        _rawSampleTransformer{rawSampleTransformer},
        _collectedSamples{memoryResource}
    {
    }

    // interfaces implementation
public:
    const char* GetName() override
    {
        return _name.c_str();
    }

    void Add(TRawSample&& sample) override
    {
        _collectedSamples.Add(std::move(sample));
    }

    std::unique_ptr<SamplesEnumerator> GetSamples() override
    {
        return std::make_unique<SamplesEnumeratorImpl>(_collectedSamples.Move(), _rawSampleTransformer, _valueOffsets);
    }

protected:
    std::chrono::nanoseconds GetCurrentTimestamp()
    {
        return OpSysTools::GetHighPrecisionTimestamp();
    }

    std::vector<SampleValueTypeProvider::Offset> const& GetValueOffsets() const
    {
        return _valueOffsets;
    }

private:
    class SamplesEnumeratorImpl : public SamplesEnumerator
    {
    public:
        SamplesEnumeratorImpl(RawSamples<TRawSample> rawSamples,
            RawSampleTransformer* rawSampleTransformer,
            std::vector<SampleValueTypeProvider::Offset> const & valueOffsets)
            :
            _rawSamples{std::move(rawSamples)},
            _rawSampleTransformer{rawSampleTransformer},
            _currentRawSample{_rawSamples.begin()},
            _valueOffsets{valueOffsets}
        {
        }

        // Inherited via SamplesEnumerator
        std::size_t size() const override
        {
            return _rawSamples.size();
        }

        bool MoveNext(std::shared_ptr<Sample>& sample) override
        {
            if (_currentRawSample == _rawSamples.end())
                return false;

            _rawSampleTransformer->Transform(*_currentRawSample, sample, _valueOffsets);
            _currentRawSample++;

            return true;
        }

    private:
        RawSamples<TRawSample> _rawSamples;
        RawSampleTransformer* _rawSampleTransformer;
        typename RawSamples<TRawSample>::iterator _currentRawSample;
        std::vector<SampleValueTypeProvider::Offset> const & _valueOffsets;
    };

    bool StartImpl() override
    {
        return true;
    }

    bool StopImpl() override
    {
        return true;
    }

private:
    std::vector<SampleValueTypeProvider::Offset> _valueOffsets;
    RawSamples<TRawSample> _collectedSamples;
    RawSampleTransformer* _rawSampleTransformer;
};
