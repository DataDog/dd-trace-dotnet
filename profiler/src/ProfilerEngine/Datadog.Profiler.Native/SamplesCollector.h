// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IConfiguration.h"
#include "IExporter.h"
#include "IMetricsSender.h"
#include "ISamplesCollector.h"
#include "IService.h"
#include "IThreadsCpuManager.h"

#include <forward_list>
#include <mutex>
#include <thread>
#include <future>

using namespace std::chrono_literals;

class SamplesCollector
    : public ISamplesCollector, public IService
{
public:
    SamplesCollector(IConfiguration* configuration, IThreadsCpuManager* pThreadsCpuManager, IExporter* exporter, IMetricsSender* metricsSender);

    // Inherited via IService
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;

    void Register(ISamplesProvider* samplesProvider) override;

    // Public but should only be called privately or from tests
    void Export();

private:
    void SamplesWork();
    void ExportWork();
    void CollectSamples();
    void SendHeartBeatMetric(bool success);

    const char* _serviceName = "SamplesCollector";
    const WCHAR* WorkerThreadName = WStr("DD.Profiler.SamplesCollector.WorkerThread");
    const WCHAR* ExporterThreadName = WStr("DD.Profiler.SamplesCollector.ExporterThread");

    inline static constexpr std::chrono::nanoseconds CollectingPeriod = 60ms;
    inline static std::string const SuccessfulExportsMetricName = "datadog.profiling.dotnet.operational.exports";

    std::chrono::seconds _uploadInterval;
    bool _mustStop;
    IThreadsCpuManager* _pThreadsCpuManager;
    std::forward_list<ISamplesProvider*> _samplesProviders;
    std::thread _workerThread;
    std::thread _exporterThread;
    std::mutex _exportLock;
    std::promise<void> _exporterThreadPromise;
    IMetricsSender* _metricsSender;
    IExporter* _exporter;
};
