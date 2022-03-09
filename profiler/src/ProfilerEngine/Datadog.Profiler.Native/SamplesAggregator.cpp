// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesAggregator.h"
#include "Configuration.h"
#include "IExporter.h"
#include "ISamplesProvider.h"
#include "Log.h"
#include "Sample.h"

#include <forward_list>
#include <list>
#include <memory>
#include <thread>

using namespace std::literals::chrono_literals;

const std::chrono::seconds SamplesAggregator::ProcessingInterval = 1s;

SamplesAggregator::SamplesAggregator(IConfiguration* configuration,
                                     IExporter* exporter) :
    _uploadInterval{configuration->GetUploadInterval()},
    _nextExportTime{std::chrono::steady_clock::now() + _uploadInterval},
    _exporter{exporter},
    _mustStop{false}
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

    return true;
}

bool SamplesAggregator::Stop()
{
    Log::Info("Stopping the samples aggregator");
    _mustStop = true;
    _worker.join();

    return true;
}


void SamplesAggregator::Register(ISamplesProvider* samplesProvider)
{
    _samplesProviders.push_front(samplesProvider);
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

void SamplesAggregator::Work()
{
    while (!_mustStop)
    {
        // TODO catch structured exception
        //      or make the library able to catch then using try/catch
        try
        {
            std::this_thread::sleep_for(ProcessingInterval);

            auto samples = CollectSamples();

            for (auto const& sample : samples)
            {
                _exporter->Add(sample);
            }

            Export();
        }
        catch (std::exception const& ex)
        {
            Log::Error("An exception occured: ", ex.what());
        }
    }
}

void SamplesAggregator::Export()
{
    auto now = std::chrono::steady_clock::now();

    // We flush the profile if we reach the time limit or we have to stop
    if (_mustStop || _nextExportTime <= now)
    {
        _exporter->Export();

        _nextExportTime = now + _uploadInterval;
    }
}
