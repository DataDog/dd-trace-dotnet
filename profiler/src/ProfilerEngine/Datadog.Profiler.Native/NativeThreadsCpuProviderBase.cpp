// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NativeThreadsCpuProviderBase.h"

#include "CpuTimeProvider.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "RawCpuSample.h"

NativeThreadsCpuProviderBase::NativeThreadsCpuProviderBase(CpuTimeProvider* cpuTimeProvider) :
    _cpuTimeProvider{cpuTimeProvider},
    _previousTotalCpuTime{0}
{
}

std::list<std::shared_ptr<Sample>> NativeThreadsCpuProviderBase::GetSamples()
{
    std::uint64_t cpuTime = 0;
    for (auto const& thread : GetThreads())
    {
        cpuTime += OsSpecificApi::GetThreadCpuTime(thread.get());
    }


    auto currentTotalCpuTime = cpuTime;
    // There is a case where it's possible to have currentTotalCpuTime < _previousTotalCpuTime: native threads died in the meantime
    // To avoid sending negative values, just check and returns 0 instead.
    cpuTime = currentTotalCpuTime >= _previousTotalCpuTime ? currentTotalCpuTime - _previousTotalCpuTime : 0;
    OnCpuDuration(cpuTime);

    // For native threads, we need to keep the last cpu time
    _previousTotalCpuTime = currentTotalCpuTime;

    auto samples = std::list<std::shared_ptr<Sample>>();
    if (cpuTime == 0)
    {
        Log::Debug(GetName(), " CPU time sums up to 0. No sample will be created.");
        return samples;
    }

    RawCpuSample rawSample;
    rawSample.Duration = cpuTime;

    // Cpu Time provider knows the offset of the Cpu value
    // So leave the transformation to it
    auto sample = _cpuTimeProvider->TransformRawSample(rawSample);

    // The resulting callstack of the transformation is empty
    // Add a fake "GC" frame to the sample
    // TODO add strings as static field ? (from framestore ?)
    for (auto frame : GetFrames())
    {
        sample->AddFrame(frame);
    }

    samples.push_back(sample);
    return samples;
}

void NativeThreadsCpuProviderBase::OnCpuDuration(std::uint64_t cpuTime)
{
}
