// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>
#include <mutex>
#include <string>
#include <thread>

#include "IService.h"
#include "ICollector.h"
#include "SamplesProvider.h"

// forward declarations
class IConfiguration;
class IFrameStore;
class IAppDomainStore;

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
template <class TRawSample>   // TODO: add a constraint TRawSample : public RawSample
class ProviderBase
    :
    public IService,
    public ICollector<TRawSample>,  // allows profilers to add TRawSample instances
    public SamplesProvider          // returns Samples to the aggregator
{
public:
    ProviderBase(IConfiguration* pConfiguration, IFrameStore* pFrameStore, IAppDomainStore* pAssemblyStore);

// interfaces implementation
public:
    // const char* GetName() must be defined by inherited class
    bool Start() override;
    bool Stop() override;
    void Add(TRawSample&& sample) override;

protected:
    // set values and additional labels
    virtual void OnTransformRawSample(const TRawSample& rawSample, Sample& sample) = 0;

private:
    void ProcessSamples();
    std::list<TRawSample> FetchRawSamples();
    void TransformRawSamples(const std::list<TRawSample>& input);
    void TransformRawSample(const TRawSample& rawSample);
    void SetAppDomainDetails(const TRawSample& rawSample, Sample& sample);
    void SetThreadDetails(const TRawSample& rawSample, Sample& sample);
    void SetStack(const TRawSample& rawSample, Sample& sample);

private:
    IFrameStore* _pFrameStore = nullptr;
    IAppDomainStore* _pAppDomainStore = nullptr;
    bool _isNativeFramesEnabled = false;

    // A thread is responsible for asynchronously fetching raw samples from the input queue
    // and feeding the output sample list with symbolized frames and thread/appdomain names
    std::atomic<bool> _stopRequested = false;
    std::thread _transformerThread;

    std::mutex _rawSamplesLock;
    std::list<TRawSample> _collectedSamples;
};
