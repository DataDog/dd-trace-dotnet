// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesCollector.h"

#include "Log.h"
#include "OpSysTools.h"

SamplesCollector::SamplesCollector(
    IConfiguration* configuration,
    IThreadsCpuManager* pThreadsCpuManager,
    IExporter* exporter,
    IMetricsSender* metricsSender) :
    _uploadInterval{configuration->GetUploadInterval()},
    _mustStop{false},
    _pThreadsCpuManager{pThreadsCpuManager},
    _metricsSender{metricsSender},
    _exporter{exporter}
{
}

void SamplesCollector::Register(ISamplesProvider* samplesProvider)
{
    _samplesProviders.push_front(samplesProvider);
}

bool SamplesCollector::Start()
{
    Log::Info("Starting the samples collector");
    _mustStop = false;
    _workerThread = std::thread(&SamplesCollector::SamplesWork, this);
    OpSysTools::SetNativeThreadName(&_workerThread, WorkerThreadName);
    _exporterThread = std::thread(&SamplesCollector::ExportWork, this);
    OpSysTools::SetNativeThreadName(&_exporterThread, ExporterThreadName);
    return true;
}

bool SamplesCollector::Stop()
{
    if (_mustStop)
    {
        return true;
    }

    _mustStop = true;
    _workerThread.join();

    _exporterThreadPromise.set_value();
    _exporterThread.join();

    return true;
}

const char* SamplesCollector::GetName()
{
    return _serviceName;
}

void SamplesCollector::SamplesWork()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), WorkerThreadName);

    while (!_mustStop)
    {
        CollectSamples();
        std::this_thread::sleep_for(CollectingPeriod);
    }
}

void SamplesCollector::ExportWork()
{
    const auto future = _exporterThreadPromise.get_future();

    while (future.wait_for(_uploadInterval) == std::future_status::timeout)
    {
        Export();
    }

    // Exiting, export one last time
    Export();
}

void SamplesCollector::Export()
{
    bool success = false;

    try
    {
        std::lock_guard lock(_exportLock);
        success = _exporter->Export();
    }
    catch (std::exception const& ex)
    {
        SendHeartBeatMetric(false);
        Log::Error("An exception occured during export: ", ex.what());
    }

    SendHeartBeatMetric(success);
    _pThreadsCpuManager->LogCpuTimes();
}

void SamplesCollector::CollectSamples()
{
    for (auto const& samplesProvider : _samplesProviders)
    {
        try
        {
            std::lock_guard lock(_exportLock);

            auto result = samplesProvider->GetSamples();

            for (auto const& sample : result)
            {
                if (!sample.GetCallstack().empty())
                {
                    _exporter->Add(sample);
                }
            }
        }
        catch (std::exception const& ex)
        {
            Log::Error("An exception occured while collecting samples: ", ex.what());
        }
    }
}

void SamplesCollector::SendHeartBeatMetric(bool success)
{
    if (_metricsSender != nullptr)
    {
        _metricsSender->Counter(SuccessfulExportsMetricName, 1, {{"success", success ? "1" : "0"}});
    }
}