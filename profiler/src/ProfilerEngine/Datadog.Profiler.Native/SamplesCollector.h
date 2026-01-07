// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IBatchedSamplesProvider.h"
#include "IConfiguration.h"
#include "IExporter.h"
#include "IMetricsSender.h"
#include "ISamplesCollector.h"
#include "IThreadsCpuManager.h"
#include "ServiceBase.h"

#include <forward_list>
#include <mutex>    
#include <thread>
#include <future>

using namespace std::chrono_literals;

namespace libdatadog {
class SymbolsStore;
}

class SamplesCollector
    :
    public ISamplesCollector,
    public ServiceBase
{
public:
    SamplesCollector(IConfiguration* configuration, IThreadsCpuManager* pThreadsCpuManager, IExporter* exporter, IMetricsSender* metricsSender, libdatadog::SymbolsStore* symbolsStore);

    // Inherited via IService
    const char* GetName() override;

    void Register(ISamplesProvider* samplesProvider) override;
    void RegisterBatchedProvider(IBatchedSamplesProvider* batchedSamplesProvider) override;

    // Public but should only be called privately or from tests
    void Export(bool lastCall = false);

    inline static constexpr std::chrono::nanoseconds CollectingPeriod = 60ms;

private:
    bool StartImpl() override;
    bool StopImpl() override;

    void SamplesWork();
    void ExportWork();
    void CollectSamples(std::forward_list<std::pair<ISamplesProvider*, uint64_t>>& samplesProviders);
    void SendHeartBeatMetric(bool success);

    const char* _serviceName = "SamplesCollector";
    const WCHAR* WorkerThreadName = WStr("DD_worker");
    const WCHAR* ExporterThreadName = WStr("DD_exporter");

    inline static std::string const SuccessfulExportsMetricName = "datadog.profiling.dotnet.operational.exports";

    std::chrono::seconds _uploadInterval;
    IThreadsCpuManager* _pThreadsCpuManager;
    std::forward_list<std::pair<ISamplesProvider*, uint64_t>> _samplesProviders;
    std::forward_list<std::pair<ISamplesProvider*, uint64_t>> _batchedSamplesProviders;
    std::thread _workerThread;
    std::thread _exporterThread;
    std::recursive_mutex _exportLock;
    std::promise<void> _exporterThreadPromise;
    std::promise<void> _workerThreadPromise;
    IMetricsSender* _metricsSender;
    IExporter* _exporter;

    // OPTIM
    // It safe to have only one cached sample with no synchronization
    // This field is only used by one thread at a time:
    // - worker thread responsible to collect and push samples in the profile
    // - thread executing the Stop: at that time, the worker thread has stopped
    //   and the thread will be the only one using this field to collect the last samples
    std::shared_ptr<Sample> _cachedSample;
};
