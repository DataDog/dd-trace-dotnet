// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesCollector.h"

#include "Log.h"
#include "OpSysTools.h"

SamplesCollector::SamplesCollector(
    IConfiguration* configuration,
    IThreadsCpuManager* pThreadsCpuManager,
    IExporter* exporter,
    IMetricsSender* metricsSender,
    IGCSuspensionsProvider* gcSuspensionProvider)
    :
    _uploadInterval{configuration->GetUploadInterval()},
    _mustStop{false},
    _pThreadsCpuManager{pThreadsCpuManager},
    _metricsSender{metricsSender},
    _exporter{exporter},
    _gcSuspensionProvider{gcSuspensionProvider}
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

    _workerThreadPromise.set_value();
    _workerThread.join();

    _exporterThreadPromise.set_value();
    _exporterThread.join();

    // Export the leftover samples
    CollectSamples();
    Export();

    return true;
}

const char* SamplesCollector::GetName()
{
    return _serviceName;
}

void SamplesCollector::SamplesWork()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), WorkerThreadName);

    const auto future = _workerThreadPromise.get_future();

    while (future.wait_for(CollectingPeriod) == std::future_status::timeout)
    {
        CollectSamples();
    }
}

void SamplesCollector::ExportWork()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), ExporterThreadName);

    const auto future = _exporterThreadPromise.get_future();

    while (future.wait_for(_uploadInterval) == std::future_status::timeout)
    {
        Export();
    }
}

void SamplesCollector::Export()
{
    bool success = false;

    try
    {
        std::lock_guard lock(_exportLock);

        // TODO: get the additional files such as GC and suspensions details
        uint8_t* pBuffer = nullptr;
        uint64_t bufferSize = 0;
        std::string filename = "suspensions.json";
        if (_gcSuspensionProvider != nullptr)
        {
            _gcSuspensionProvider->GetSuspensions(pBuffer, bufferSize);
        }
        success = _exporter->Export(filename, pBuffer, bufferSize);
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