// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <chrono>
#include <string>
#include "Log.h"
#include "OpSysTools.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IAppDomainStore.h"
#include "RawSample.h"
#include "ProviderBase.h"

#include "shared/src/native-src/string.h"

using namespace std::chrono_literals;
constexpr std::chrono::nanoseconds CollectingPeriod = 50ms;

template <class TRawSample>
ProviderBase<TRawSample>::ProviderBase(IConfiguration* pConfiguration, IFrameStore* pFrameStore, IAppDomainStore* pAppDomainStore) :
    _isNativeFramesEnabled{pConfiguration->IsNativeFramesEnabled()},
    _pFrameStore{pFrameStore},
    _pAppDomainStore{pAppDomainStore}
{
}

template <class TRawSample>
bool ProviderBase<TRawSample>::Start()
{
    _transformerThread = std::thread(&ProviderBase<TRawSample>::ProcessSamples, this);

    std::wstringstream builder;
    builder << WStr("DD.Profiler.") << GetName() << WStr(".Thread");
    OpSysTools::SetNativeThreadName(&_transformerThread, builder.str());

    return true;
}

template <class TRawSample>
bool ProviderBase<TRawSample>::Stop()
{
    _stopRequested.store(true);
    _transformerThread.join();

    return true;
}

template <class TRawSample>
void ProviderBase<TRawSample>::Add(TRawSample&& sample)
{
    std::lock_guard<std::mutex> lock(_rawSamplesLock);

    _collectedSamples.push_back(std::move(sample));
}

template <class TRawSample>
void ProviderBase<TRawSample>::ProcessSamples()
{
    std::stringstream startBuilder;
    startBuilder << "Starting to process raw '" << GetName() << "' samples.";
    Log::Info(startBuilder.str());

    while (!_stopRequested.load())
    {
        std::list<TRawSample> input = FetchRawSamples();
        if (input.size() != 0)
        {
            TransformRawSamples(input);
        }

        // TODO: instead of sleeping, we could wait on an event
        //       that would be set in the Add() method
        std::this_thread::sleep_for(CollectingPeriod);
    }

    std::stringstream stopBuilder;
    stopBuilder << "Stop processing raw '" << GetName() << "' samples.";
    Log::Info(stopBuilder.str());
}

template <class TRawSample>
std::list<TRawSample> ProviderBase<TRawSample>::FetchRawSamples()
{
    std::lock_guard<std::mutex> lock(_rawSamplesLock);

    std::list<TRawSample> input = std::move(_collectedSamples); // _collectedSamples is empty now
    return input;
}

template <class TRawSample>
void ProviderBase<TRawSample>::TransformRawSamples(const std::list<TRawSample>& input)
{
    for (auto const& rawSample : input)
    {
        TransformRawSample(rawSample);
    }
}

template <class TRawSample>
void ProviderBase<TRawSample>::TransformRawSample(const TRawSample& rawSample)
{
    Sample sample;
    const RawSample& rSample = static_cast<const RawSample&>(rawSample);
    if (rSample.LocalRootSpanId != 0 && rSample.spanId != 0)
    {
        AddLabel(Label{Sample::LocalRootSpanIdLabel, std::to_string(rSample.LocalRootSpanId)});
        AddLabel(Label{Sample::SpanIdLabel, std::to_string(rSample.SpanId)});
    }

    // compute thread/appdomain details
    SetAppDomainDetails(rSample, sample);
    SetThreadDetails(rSample, sample);

    // compute symbols for frames
    SetStack(rSample, sample);

    // allow inherited classes to add values and specific labels
    OnTransformRawSample(rawSample, sample);

    // save it in the output list
    Store(std::move(sample));
}

template <class TRawSample>
void ProviderBase<TRawSample>::SetStack(const TRawSample& rawSample, Sample& sample)
{
    const RawSample& rSample = static_cast<const RawSample&>(rawSample);
    for (auto const& instructionPointer : rSample.Stack)
    {
        auto [isManaged, moduleName, frame] = _pFrameStore->GetFrame(instructionPointer);

        // filter out native frames if needed
        if (isManaged || _isNativeFramesEnabled)
        {
            sample.AddFrame(moduleName, frame);
        }
    }
}

template <class TRawSample>
void ProviderBase<TRawSample>::SetThreadDetails(const TRawSample& rawSample, Sample& sample)
{
    const RawSample& rSample = static_cast<const RawSample&>(rawSample);

    // needed for tests
    if (rSample.ThreadInfo == nullptr)
    {
        sample.SetThreadId("<0> [# 0]");
        sample.SetThreadName("Managed thread (name unknown) [#0]");

        return;
    }

    // build the ID
    std::stringstream builder;
    auto profTid = rSample.ThreadInfo->GetProfilerThreadInfoId();
    auto osTid = rSample.ThreadInfo->GetOsThreadId();
    builder << "<" << std::dec << profTid << "> [#" << osTid << "]";
    sample.SetThreadId(builder.str());

    // build the name
    std::stringstream nameBuilder;
    if (rSample.ThreadInfo->GetThreadName().empty())
    {
        nameBuilder << "Managed thread (name unknown)";
    }
    else
    {
        nameBuilder << shared::ToString(rSample.ThreadInfo->GetThreadName());
    }
    nameBuilder << " [#" << rSample.ThreadInfo->GetOsThreadId() << "]";
    sample.SetThreadName(nameBuilder.str());

    // don't forget to release the ManagedThreadInfo
    rSample.ThreadInfo->Release();
}

void SetEmptyAppDomainDetailsX(Sample& sample)
{
    sample.SetAppDomainName("");
    sample.SetPid("0");
}

template <class TRawSample>
void ProviderBase<TRawSample>::SetAppDomainDetails(const TRawSample& rawSample, Sample& sample)
{
    const RawSample& rSample = static_cast<const RawSample&>(rawSample);
    ProcessID pid;
    std::string appDomainName;

    if (!_pAppDomainStore->GetInfo(rSample.AppDomainId, pid, appDomainName))
    {
        SetEmptyAppDomainDetailsX(sample);
        return;
    }

    sample.SetAppDomainName(appDomainName);

    std::stringstream builder;
    builder << std::dec << pid;
    sample.SetPid(builder.str());
}
