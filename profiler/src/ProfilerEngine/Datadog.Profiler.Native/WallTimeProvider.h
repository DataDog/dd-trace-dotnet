// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>
#include <mutex>
#include <string>
#include <thread>

#include "IWallTimeCollector.h"
#include "SamplesProvider.h"
#include "WallTimeSampleRaw.h"
#include "WallTimeSample.h"

// forward declarations
class IConfiguration;
class IFrameStore;
class IAppDomainStore;


// IDEA: could be a templated class for all providers
//       that manages the input queue and the processing thread
//
//  SampleProvider<T>
//  {
//  ...
//  private:
//      std::list<T> _collectedSamples;
//  ...
//  }
//
class WallTimeProvider
    :
    public IWallTimeCollector,  // accepts raw walltime samples
    public SamplesProvider      // returns Samples to the aggregator
{
public:
    WallTimeProvider(IConfiguration* pConfiguration, IFrameStore* pFrameStore, IAppDomainStore* pAssemblyStore);

// interfaces implementation
public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    void Add(WallTimeSampleRaw&& sample) override;

private:
    void ProcessSamples();
    std::list<WallTimeSampleRaw> FetchRawSamples();
    void TransformRawSamples(const std::list<WallTimeSampleRaw>& input);
    void TransformRawSample(const WallTimeSampleRaw& rawSample);
    void SetAppDomainDetails(const WallTimeSampleRaw& rawSample, WallTimeSample& sample);
    void SetThreadDetails(const WallTimeSampleRaw& rawSample, WallTimeSample& sample);
    void SetStack(const WallTimeSampleRaw& rawSample, WallTimeSample& sample);

private:
    const char* _serviceName = "WallTimeProvider";
    IFrameStore* _pFrameStore = nullptr;
    IAppDomainStore* _pAppDomainStore = nullptr;
    bool _isNativeFramesEnabled = false;

    // A thread is responsible for asynchronously fetching raw samples from the input queue
    // and feeding the output sample list with symbolized frames and thread/appdomain names
    std::atomic<bool> _stopRequested = false;
    std::thread _transformerThread;

    std::mutex _rawSamplesLock;
    std::list<WallTimeSampleRaw> _collectedSamples;
};
