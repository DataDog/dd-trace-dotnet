// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesCollector.h"

#include "Log.h"
#include "OpSysTools.h"


SamplesCollector::SamplesCollector(IThreadsCpuManager* pThreadsCpuManager) :
    _pThreadsCpuManager(pThreadsCpuManager)
{
}

void SamplesCollector::Register(ISamplesProvider* samplesProvider)
{
    _samplesProviders.push_front(samplesProvider);
}

std::list<Sample> SamplesCollector::GetSamples()
{
    std::lock_guard<std::mutex> lock(_samplesLock);

    return std::move(_samples);
}

bool SamplesCollector::Start()
{
    Log::Info("Starting the samples collector");
    _mustStop = false;
    _worker = std::thread(&SamplesCollector::Work, this);
    OpSysTools::SetNativeThreadName(&_worker, WorkerThreadName);
    return true;
}

bool SamplesCollector::Stop()
{
    if (_mustStop)
    {
        return true;
    }

    _mustStop = true;
    _worker.join();

    return true;
}

const char* SamplesCollector::GetName()
{
    return _serviceName;
}

void SamplesCollector::Work()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), WorkerThreadName);

    while (!_mustStop)
    {
        CollectSamples();
        std::this_thread::sleep_for(CollectingPeriod);
    }
}

void SamplesCollector::CollectSamples()
{
    for (auto const& samplesProvider : _samplesProviders)
    {
        auto result = samplesProvider->GetSamples();

        std::lock_guard<std::mutex> lock(_samplesLock);

        _samples.splice(_samples.cend(), result);
    }
}
