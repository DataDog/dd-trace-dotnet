// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesAggregator.h"

#include "Configuration.h"
#include "IExporter.h"
#include "IMetricsSender.h"
#include "ISamplesCollector.h"
#include "ISamplesProvider.h"
#include "IThreadsCpuManager.h"
#include "Log.h"
#include "OpSysTools.h"
#include "Sample.h"

#include <forward_list>
#include <list>
#include <memory>
#include <thread>

using namespace std::literals::chrono_literals;

SamplesAggregator::SamplesAggregator(IConfiguration* configuration,
                                     IThreadsCpuManager* pThreadsCpuManager,
                                     IExporter* exporter,
                                     IMetricsSender* metricsSender,
                                     ISamplesCollector* samplesCollector) :
    _uploadInterval{configuration->GetUploadInterval()},
    _nextExportTime{std::chrono::steady_clock::now() + _uploadInterval},
    _exporter{exporter},
    _pSamplesCollector{samplesCollector},
    _pThreadsCpuManager{pThreadsCpuManager},
    _mustStop{false},
    _metricsSender{metricsSender}
{
}

const char* SamplesAggregator::GetName()
{
    return _serviceName;
}

bool SamplesAggregator::Start()
{
    Log::Info("Starting the samples aggregator");
    _mustStop = false;
    _worker = std::thread(&SamplesAggregator::Work, this);
    OpSysTools::SetNativeThreadName(&_worker, WorkerThreadName);
    return true;
}

bool SamplesAggregator::Stop()
{
    if (_mustStop)
    {
        return true;
    }

    Log::Info("Stopping the samples aggregator");
    _mustStop = true;
    _exitWorkerPromise.set_value();
    _worker.join();

    // Process leftover samples
    try
    {
        ProcessSamples();
    }
    catch (std::exception const& ex)
    {
        SendHeartBeatMetric(false);
        Log::Error("An exception occured: ", ex.what());
    }

    return true;
}

void SamplesAggregator::Work()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), WorkerThreadName);

    const auto future = _exitWorkerPromise.get_future();

    while (future.wait_for(ProcessingInterval) == std::future_status::timeout)
    {
        // TODO catch structured exception
        //      or make the library able to catch then using try/catch
        try
        {
            ProcessSamples();
        }
        catch (std::exception const& ex)
        {
            SendHeartBeatMetric(false);
            Log::Error("An exception occured: ", ex.what());
        }
    }
    // When the aggregator is stopped, a last .pprof is exported
}

void SamplesAggregator::ProcessSamples()
{
    const auto samples = _pSamplesCollector->GetSamples();

    for (auto const& sample : samples)
    {
        if (!sample.GetCallstack().empty())
        {
            _exporter->Add(sample);
        }
    }

    Export();
}

void SamplesAggregator::Export()
{
    auto now = std::chrono::steady_clock::now();

    // We flush the profile if we reach the time limit or we have to stop
    if (_mustStop || _nextExportTime <= now)
    {
        _nextExportTime = now + _uploadInterval;

        auto success = _exporter->Export();

        SendHeartBeatMetric(success);
        _pThreadsCpuManager->LogCpuTimes();
    }
}

void SamplesAggregator::SendHeartBeatMetric(bool success)
{
    if (_metricsSender != nullptr)
    {
        _metricsSender->Counter(SuccessfulExportsMetricName, 1, {{"success", success ? "1" : "0"}});
    }
}
