// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "SampleEnumerator.h"

#include <list>
#include <memory>

class FakeSamples : public SampleEnumerator
{
public:
    FakeSamples();
    ~FakeSamples() = default;
    FakeSamples(std::shared_ptr<Sample> sample);
    FakeSamples(std::list<std::shared_ptr<Sample>> samples);

    // Inherited via SampleEnumerator
    std::size_t size() const override;

    bool MoveNext(std::shared_ptr<Sample>& sample) override;

    std::list<std::shared_ptr<Sample>> _samples;
    std::list<std::shared_ptr<Sample>>::iterator _currentPos;
};