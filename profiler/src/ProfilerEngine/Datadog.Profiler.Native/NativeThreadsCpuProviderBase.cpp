// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NativeThreadsCpuProviderBase.h"

#include "CpuTimeProvider.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "RawCpuSample.h"
#include "SamplesEnumerator.h"

NativeThreadsCpuProviderBase::NativeThreadsCpuProviderBase(CpuTimeProvider* cpuTimeProvider) :
    _cpuTimeProvider{cpuTimeProvider},
    _previousTotalCpuTime{0}
{
}

class CpuSampleEnumerator : public SamplesEnumerator
{
public:
    CpuSampleEnumerator() :
        _sample{nullptr}
    {
    }

    CpuSampleEnumerator(CpuSampleEnumerator const&) = delete;
    CpuSampleEnumerator& operator=(CpuSampleEnumerator const&) = delete;

    void Set(std::shared_ptr<Sample> sample)
    {
        _sample = std::move(sample);
    }

    // Inherited via SamplesEnumerator
    std::size_t size() const override
    {
        return _sample == nullptr ? 0 : 1;
    }

    bool MoveNext(std::shared_ptr<Sample>& sample) override
    {
        if (_sample == nullptr)
        {
            return false;
        }
        // not thread-safe but ok since this enumerator will be consumed by only one thread (when exporting)
        sample = _sample;
        _sample.reset();

        return true;
    }

    std::shared_ptr<Sample> _sample;
};

std::unique_ptr<SamplesEnumerator> NativeThreadsCpuProviderBase::GetSamples()
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

    auto enumerator = std::make_unique<CpuSampleEnumerator>();
    if (cpuTime == 0)
    {
        Log::Debug(GetName(), " CPU time sums up to 0. No sample will be created.");
        return enumerator;
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

    enumerator->Set(sample);

    return enumerator;
}

void NativeThreadsCpuProviderBase::OnCpuDuration(std::uint64_t cpuTime)
{
}
