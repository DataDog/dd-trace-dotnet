// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>
#include <mutex>
#include <string>
#include <thread>
#include "Log.h"
#include "OpSysTools.h"

#include "IService.h"
#include "ICollector.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IAppDomainStore.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "ProviderBase.h"
#include "RawSample.h"

#include "shared/src/native-src/string.h"

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
template <class TRawSample>   // TRawSample is supposed to inherit from RawSample
class CollectorBase
    :
    public IService,
    public ICollector<TRawSample>,  // allows profilers to add TRawSample instances
    public ProviderBase          // returns Samples to the aggregator
{
public:
    CollectorBase<TRawSample>(
        const char* name,
        IThreadsCpuManager* pThreadsCpuManager,
        IFrameStore* pFrameStore,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore
        ) :
        ProviderBase(name),
        _pFrameStore{pFrameStore},
        _pAppDomainStore{pAppDomainStore},
        _pRuntimeIdStore{pRuntimeIdStore},
        _pThreadsCpuManager{pThreadsCpuManager}
    {
    }

// interfaces implementation
public:
    bool Start() override
    {
        return true;
    }

    bool Stop() override
    {
        return true;
    }

    const char* GetName() override
    {
        return _name.c_str();
    }

    void Add(TRawSample&& sample) override
    {
        std::lock_guard<std::mutex> lock(_rawSamplesLock);

        _collectedSamples.push_back(std::forward<TRawSample>(sample));
    }

    inline std::list<Sample> GetSamples() override
    {
        std::list<TRawSample> input = FetchRawSamples();
                
        return TransformRawSamples(input);
    }

private:

    std::list<TRawSample> FetchRawSamples()
    {
        std::lock_guard<std::mutex> lock(_rawSamplesLock);

        std::list<TRawSample> input = std::move(_collectedSamples); // _collectedSamples is empty now
        return input;
    }

    std::list<Sample> TransformRawSamples(const std::list<TRawSample>& input)
    {
        std::list<Sample> samples;

        for (auto const& rawSample : input)
        {
            samples.push_back(TransformRawSample(rawSample));
        }

        return samples;
    }

    Sample TransformRawSample(const TRawSample& rawSample)
    {
        Sample sample(rawSample.Timestamp, _pRuntimeIdStore->GetId(rawSample.AppDomainId));
        if (rawSample.LocalRootSpanId != 0 && rawSample.SpanId != 0)
        {
            sample.AddLabel(Label{Sample::LocalRootSpanIdLabel, std::to_string(rawSample.LocalRootSpanId)});
            sample.AddLabel(Label{Sample::SpanIdLabel, std::to_string(rawSample.SpanId)});
        }

        // compute thread/appdomain details
        SetAppDomainDetails(rawSample, sample);
        SetThreadDetails(rawSample, sample);

        // compute symbols for frames
        SetStack(rawSample, sample);

        // allow inherited classes to add values and specific labels
        rawSample.OnTransform(sample);

        return sample;
    }

    void SetAppDomainDetails(const TRawSample& rawSample, Sample& sample)
    {
        ProcessID pid;
        std::string appDomainName;

        if (!_pAppDomainStore->GetInfo(rawSample.AppDomainId, pid, appDomainName))
        {
            sample.SetAppDomainName("");
            sample.SetPid("0");

            return;
        }

        sample.SetAppDomainName(appDomainName);

        std::stringstream builder;
        builder << std::dec << pid;
        sample.SetPid(builder.str());
    }

    void SetThreadDetails(const TRawSample& rawSample, Sample& sample)
    {
        // needed for tests
        if (rawSample.ThreadInfo == nullptr)
        {
            sample.SetThreadId("<0> [# 0]");
            sample.SetThreadName("Managed thread (name unknown) [#0]");

            return;
        }

        // build the ID
        std::stringstream builder;
        auto profTid = rawSample.ThreadInfo->GetProfilerThreadInfoId();
        auto osTid = rawSample.ThreadInfo->GetOsThreadId();
        builder << "<" << std::dec << profTid << "> [#" << osTid << "]";
        sample.SetThreadId(builder.str());

        // build the name
        std::stringstream nameBuilder;
        if (rawSample.ThreadInfo->GetThreadName().empty())
        {
            nameBuilder << "Managed thread (name unknown)";
        }
        else
        {
            nameBuilder << shared::ToString(rawSample.ThreadInfo->GetThreadName());
        }
        nameBuilder << " [#" << rawSample.ThreadInfo->GetOsThreadId() << "]";
        sample.SetThreadName(nameBuilder.str());

        // don't forget to release the ManagedThreadInfo
        rawSample.ThreadInfo->Release();
    }

    void SetStack(const TRawSample& rawSample, Sample& sample)
    {
        for (auto const& instructionPointer : rawSample.Stack)
        {
            auto [isResolved, moduleName, frame] = _pFrameStore->GetFrame(instructionPointer);

            if (isResolved)
            {
                sample.AddFrame(moduleName, frame);
            }
        }
    }

private:
    IFrameStore* _pFrameStore = nullptr;
    IAppDomainStore* _pAppDomainStore = nullptr;
    IRuntimeIdStore* _pRuntimeIdStore = nullptr;
    IThreadsCpuManager* _pThreadsCpuManager = nullptr;
    bool _isNativeFramesEnabled = false;

    // A thread is responsible for asynchronously fetching raw samples from the input queue
    // and feeding the output sample list with symbolized frames and thread/appdomain names
    std::atomic<bool> _stopRequested = false;

    std::mutex _rawSamplesLock;
    std::list<TRawSample> _collectedSamples;
};
