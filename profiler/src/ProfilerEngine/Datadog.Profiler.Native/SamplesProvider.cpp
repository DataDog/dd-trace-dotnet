// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesProvider.h"
#include "Sample.h"


void SamplesProvider::Store(Sample&& sample)
{
    std::lock_guard<std::mutex> lock(_samplesLock);

    _samples.push_back(std::move(sample));
}


std::list<Sample> SamplesProvider::GetSamples()
{
    std::lock_guard<std::mutex> lock(_samplesLock);

    auto samplesToReturn = std::move(_samples);  // _samples is empty now
    return samplesToReturn;
}
