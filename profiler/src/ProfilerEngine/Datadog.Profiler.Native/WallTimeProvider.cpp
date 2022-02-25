// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <chrono>
#include <string>
#include "OpSysTools.h"
#include "Log.h"
#include "WallTimeProvider.h"
#include "WallTimeSampleRaw.h"
#include "WallTimeSample.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IAppDomainStore.h"

#include "shared/src/native-src/string.h"

using namespace std::chrono_literals;
constexpr std::chrono::nanoseconds CollectingPeriod = 50ms;


WallTimeProvider::WallTimeProvider(IConfiguration* pConfiguration, IFrameStore* pFrameStore, IAppDomainStore* pAppDomainStore)
    :
    _isNativeFramesEnabled{pConfiguration->IsNativeFramesEnabled()},
    _pFrameStore{pFrameStore},
    _pAppDomainStore{pAppDomainStore}
{
}

const char* WallTimeProvider::GetName()
{
    return _serviceName;
}

bool WallTimeProvider::Start()
{
    _transformerThread = std::thread(&WallTimeProvider::ProcessSamples, this);
    OpSysTools::SetNativeThreadName(&_transformerThread,  WStr("DD.Profiler.WallTimeProvider.Thread"));

    return true;
}

bool WallTimeProvider::Stop()
{
    _stopRequested.store(true);
    _transformerThread.join();

    return true;
}

void WallTimeProvider::Add(WallTimeSampleRaw&& sample)
{
    std::lock_guard<std::mutex> lock(_rawSamplesLock);

    _collectedSamples.push_back(std::move(sample));
}


void WallTimeProvider::ProcessSamples()
{
    Log::Info("Starting to process raw WallTime samples.");

    while (!_stopRequested.load())
    {
        std::list<WallTimeSampleRaw> input = FetchRawSamples();
        if (input.size() != 0)
        {
            TransformRawSamples(input);
        }

        // TODO: instead of sleeping, we could wait on an event
        //       that would be set in the Add() method
        std::this_thread::sleep_for(CollectingPeriod);
    }

    Log::Info("Stop processing raw WallTime samples.");
}

std::list<WallTimeSampleRaw> WallTimeProvider::FetchRawSamples()
{
    std::lock_guard<std::mutex> lock(_rawSamplesLock);

    std::list<WallTimeSampleRaw> input = std::move(_collectedSamples);  // _collectedSamples is empty now
    return input;
}

void WallTimeProvider::TransformRawSamples(const std::list<WallTimeSampleRaw>& input)
{
    for (auto const& rawSample : input)
    {
        TransformRawSample(rawSample);
    }
}

void WallTimeProvider::TransformRawSample(const WallTimeSampleRaw& rawSample)
{
    WallTimeSample sample(rawSample.Timestamp, rawSample.Duration, rawSample.TraceId, rawSample.SpanId);

    // compute thread/appdomain details
    SetAppDomainDetails(rawSample, sample);
    SetThreadDetails(rawSample, sample);

    // compute symbols for frames
    SetStack(rawSample, sample);

    // save it in the output list
    Store(std::move(sample));
}

void WallTimeProvider::SetStack(const WallTimeSampleRaw& rawSample, WallTimeSample& sample)
{
    for (auto const& instructionPointer : rawSample.Stack)
    {
        auto [isManaged, moduleName, frame] = _pFrameStore->GetFrame(instructionPointer);

        // filter out native frames if needed
        if (isManaged || _isNativeFramesEnabled)
        {
            sample.AddFrame(moduleName, frame);
        }
    }
}

void WallTimeProvider::SetThreadDetails(const WallTimeSampleRaw& rawSample, WallTimeSample& sample)
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

void SetEmptyAppDomainDetails(WallTimeSample& sample)
{
    sample.SetAppDomainName("");
    sample.SetPid("0");
}

void WallTimeProvider::SetAppDomainDetails(const WallTimeSampleRaw& rawSample, WallTimeSample& sample)
{
    std::string appDomainName;
    ProcessID pid;

    if (!_pAppDomainStore->GetInfo(rawSample.AppDomainId, pid, appDomainName))
    {
        SetEmptyAppDomainDetails(sample);
        return;
    }

    sample.SetAppDomainName(appDomainName);

    std::stringstream builder;
    builder << std::dec << pid;
    sample.SetPid(builder.str());
}
