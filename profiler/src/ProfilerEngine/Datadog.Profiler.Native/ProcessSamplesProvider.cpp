// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProcessSamplesProvider.h"

#include "CpuTimeProvider.h"
#include "IGarbageCollectorInfo.h"
#include "OsSpecificApi.h"
#include "RawCpuSample.h"

ProcessSamplesProvider::ProcessSamplesProvider(IGarbageCollectorInfo* gcInfo, CpuTimeProvider* cpuTimeProvider) :
    _gcInfo{gcInfo},
    _cpuTimeProvider{cpuTimeProvider}
{
}

std::list<std::shared_ptr<Sample>> ProcessSamplesProvider::GetSamples()
{
    std::list<std::shared_ptr<Sample>> samples;

    if (_gcInfo != nullptr && _cpuTimeProvider != nullptr)
    {
        Log::Debug("Get GC Threads CPU Sample.");
        CollectGcThreadsSamples(samples);
    }

    // TODO CollectProfilerThreadsSamples

    return samples;
}

const char* ProcessSamplesProvider::GetName()
{
    return "Process-level samples provider";
}

void ProcessSamplesProvider::CollectGcThreadsSamples(std::list<std::shared_ptr<Sample>>& samples)
{
    std::int64_t value = GetGcThreadsCpuTime();

    if (value == 0)
    {
        Log::Debug("GC Threads CPU time sums up to 0. No sample will be created.");
        return;
    }

    RawCpuSample rawSample;
    rawSample.Duration = value;

    // Cpu Time provider knows the offset of the Cpu value
    // So leave the transformation to it
    auto sample = _cpuTimeProvider->TransformRawSample(rawSample);

    // The resulting callstack of the transformation is empty
    // Add a fake "GC" frame to the sample
    // TODO add strings as static field ? (from framestore ?)
    sample->AddFrame({"CLR", "|lm: |ns: |ct: |fn:Garbage Collector", "", 0});

    samples.push_back(sample);
}

std::uint64_t ProcessSamplesProvider::GetGcThreadsCpuTime()
{
    std::uint64_t cpuTime = 0;
    for (auto const& thread : _gcInfo->GetThreads())
    {
        cpuTime += OsSpecificApi::GetThreadCpuTime(thread.get());
    }
    return cpuTime;
}
