// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstddef>
#include <memory>

class Sample;

class SamplesEnumerator
{
public:
    virtual ~SamplesEnumerator() = default;

    virtual std::size_t size() const = 0;
    virtual bool MoveNext(std::shared_ptr<Sample>& sample) = 0;
};
