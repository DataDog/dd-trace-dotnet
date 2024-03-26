// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <cstdint>
#include "cor.h"
#include <memory>

#include "RawSample.h"

class RawWallTimeSample : public RawSample
{
public:
    RawWallTimeSample() = default;

    RawWallTimeSample(RawWallTimeSample&& other) noexcept
        :
        RawSample(std::move(other)),
        Duration(other.Duration)
    {
    }

    RawWallTimeSample& operator=(RawWallTimeSample&& other) noexcept
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
        sample->AddValue(Duration, valueOffsets[0]);
    }

    std::uint64_t Duration; // in nanoseconds
};
