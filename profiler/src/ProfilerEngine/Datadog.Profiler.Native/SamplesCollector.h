// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "ISamplesCollector.h"
#include "IService.h"
#include "IThreadsCpuManager.h"

#include <forward_list>
#include <mutex>
#include <thread>

using namespace std::chrono_literals;

class SamplesCollector
    : public ISamplesCollector, public IService
{
public:
    SamplesCollector(IThreadsCpuManager* pThreadsCpuManager);

    // Inherited via IService
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;

    void Register(ISamplesProvider* samplesProvider) override;
    std::list<Sample> GetSamples() override;

private:
    void Work();
    void CollectSamples();

    const char* _serviceName = "SamplesCollector";
    const WCHAR* WorkerThreadName = WStr("DD.Profiler.SamplesCollector.WorkerThread");

    inline static constexpr std::chrono::nanoseconds CollectingPeriod = 60ms;

    bool _mustStop;
    IThreadsCpuManager* _pThreadsCpuManager;
    std::forward_list<ISamplesProvider*> _samplesProviders;
    std::thread _worker;
    std::list<Sample> _samples;
    std::mutex _samplesLock;
};
