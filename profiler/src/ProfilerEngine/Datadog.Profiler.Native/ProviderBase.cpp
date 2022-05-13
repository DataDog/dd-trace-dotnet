// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProviderBase.h"
#include "Log.h"
#include "Sample.h"

ProviderBase::ProviderBase(const char* name)
    :
    _name {name}
{
}

void ProviderBase::Store(Sample&& sample)
{
    std::lock_guard<std::mutex> lock(_samplesLock);

    _samples.push_back(std::move(sample));
}


std::list<Sample> ProviderBase::GetSamples()
{
    std::lock_guard<std::mutex> lock(_samplesLock);

    auto samplesToReturn = std::move(_samples);  // _samples is empty now

#if _DEBUG
    Log::Info("Provider '", _name, "' --> ", samplesToReturn.size(), " samples.");
#endif

    return samplesToReturn;
}
