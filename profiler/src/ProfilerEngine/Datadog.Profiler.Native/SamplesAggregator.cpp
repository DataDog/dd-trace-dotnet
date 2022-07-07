// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesAggregator.h"

#include "Configuration.h"
#include "IExporter.h"
#include "IMetricsSender.h"
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

const std::chrono::seconds SamplesAggregator::ProcessingInterval = 1s;
const WCHAR* WorkerThreadName = WStr("DD.Profiler.SamplesAggregator.WorkerThread");
const WCHAR* RawSampleThreadName = WStr("DD.Profiler.SamplesAggregator.RawSampleThread");
std::string const SamplesAggregator::SuccessfulExportsMetricName = "datadog.profiling.dotnet.operational.exports";

SamplesAggregator::SamplesAggregator(IConfiguration* configuration,
                                     IThreadsCpuManager* pThreadsCpuManager,
                                     IExporter* exporter,
                                     IMetricsSender* metricsSender) :
    _uploadInterval{configuration->GetUploadInterval()},
    _nextExportTime{std::chrono::steady_clock::now() + _uploadInterval},
    _pThreadsCpuManager{pThreadsCpuManager},
    _exporter{exporter},
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
    _worker = std::thread(&SamplesAggregator::MainWorker, this);
    _transformerThread = std::thread(&SamplesAggregator::RawSamplesWorker, this);
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
    _transformerThread.join();
    _worker.join();

    // Process leftover samples
    ProcessRawSamples();
    ProcessSamples();

    return true;
}

void SamplesAggregator::Register(ISamplesProvider* samplesProvider)
{
    _samplesProviders.push_front(samplesProvider);
}

void SamplesAggregator::MainWorker()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), WorkerThreadName);

    while (!_mustStop)
    {
        // TODO catch structured exception
        //      or make the library able to catch then using try/catch
        try
        {
            std::this_thread::sleep_for(ProcessingInterval);

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

void SamplesAggregator::RawSamplesWorker()
{
    _pThreadsCpuManager->Map(OpSysTools::GetThreadId(), RawSampleThreadName);

    while (!_mustStop)
    {
        ProcessRawSamples();
        std::this_thread::sleep_for(CollectingPeriod);
    }
}

void SamplesAggregator::ProcessSamples()
{
    auto samples = CollectSamples();
    for (auto const& sample : samples)
    {
        if (!sample.GetCallstack().empty())
        {
            _exporter->Add(sample);
        }
    }
    Export();
}

void SamplesAggregator::ProcessRawSamples()
{
    for (auto const& samplesProvider : _samplesProviders)
    {
        samplesProvider->ProcessRawSamples();
    }
}

std::list<Sample> SamplesAggregator::CollectSamples()
{
    auto result = std::list<Sample>{};

    for (auto const& samplesProvider : _samplesProviders)
    {
        result.splice(result.cend(), samplesProvider->GetSamples());
    }

    return result;
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
