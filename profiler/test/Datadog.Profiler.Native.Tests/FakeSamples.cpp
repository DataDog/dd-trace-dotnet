// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FakeSamples.h"

FakeSamples::FakeSamples()
{
    _currentPos = _samples.end();
}

FakeSamples::FakeSamples(std::shared_ptr<Sample> sample)
{
    _currentPos = _samples.begin();
    _samples.push_back(sample);
}

FakeSamples::FakeSamples(std::list<std::shared_ptr<Sample>> samples) :
    _samples{std::move(samples)}
{
    _currentPos = _samples.begin();
}

std::size_t FakeSamples::size() const
{
    return _samples.size();
}

bool FakeSamples::MoveNext(std::shared_ptr<Sample>& sample)
{
    if (_currentPos == _samples.end())
        return false;

    sample = *_currentPos;
    _currentPos++;
    return true;
}
