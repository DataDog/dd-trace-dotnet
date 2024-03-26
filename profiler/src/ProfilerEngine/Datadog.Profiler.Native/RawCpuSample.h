// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawCpuSample : public RawSample
{
public:
    RawCpuSample() noexcept = default;

    RawCpuSample(RawCpuSample&& other) noexcept
        :
        RawSample(std::move(other)),
        Duration(other.Duration)
    {
    }

    RawCpuSample& operator=(RawCpuSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            Duration = other.Duration;
        }
        return *this;
    }

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
    {
        assert(valueOffsets.size() == 1);
        sample->AddValue(Duration * 1000000, valueOffsets[0]);
    }

    std::uint64_t Duration; // in milliseconds
};
