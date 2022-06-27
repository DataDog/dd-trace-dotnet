// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <chrono>
#include <forward_list>
#include <list>
#include <memory>
#include <string>
#include <thread>
#include <chrono>
#include <future>

#include "IService.h"
#include <shared/src/native-src/string.h>

class ISamplesCollector;
class Sample;
class IConfiguration;
class IExporter;
class IMetricsSender;
class IProfileFactory;
class ISamplesProvider;
class IThreadsCpuManager;

using namespace std::literals::chrono_literals;

class SamplesAggregator : public IService
{
public:
    SamplesAggregator(IConfiguration* configuration, IThreadsCpuManager* pThreadsCpuManager, IExporter* exporter, IMetricsSender* metricsSender, ISamplesCollector* samplesCollector);

    // Inherited via IService
    virtual const char* GetName() override;
    virtual bool Start() override;
    virtual bool Stop() override;

private:
    void Work();
    void ProcessSamples();
    void Export();
    void SendHeartBeatMetric(bool success);

private:
    const char* _serviceName = "SamplesAggregator";
    inline static const std::chrono::seconds ProcessingInterval = 1s;
    const WCHAR* WorkerThreadName = WStr("DD.Profiler.SamplesAggregator.WorkerThread");
    const WCHAR* RawSampleThreadName = WStr("DD.Profiler.SamplesAggregator.RawSampleThread");
    inline static std::string const SuccessfulExportsMetricName = "datadog.profiling.dotnet.operational.exports";

    std::chrono::seconds _uploadInterval;
    std::chrono::time_point<std::chrono::steady_clock> _nextExportTime;
    IExporter* _exporter;
    IThreadsCpuManager* _pThreadsCpuManager;
    ISamplesCollector* _pSamplesCollector;
    std::thread _worker;
    bool _mustStop;
    std::promise<void> _exitWorkerPromise;
    IMetricsSender* _metricsSender;
};
