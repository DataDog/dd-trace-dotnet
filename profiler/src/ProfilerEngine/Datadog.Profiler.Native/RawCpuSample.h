// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawCpuSample : public RawSample
{
public:
    inline void OnTransform(std::shared_ptr<Sample>& sample, uint32_t valueOffset) const override
    {
        sample->AddValue(Duration * 1000000, valueOffset);
        sample->AddNumericLabel(NumericLabel{Sample::StartTimestampLabel, LastTimestamp});
    }

    std::uint64_t Duration;  // in milliseconds

    // Keep track of the last time the CPU was checked for a given thread.
    // Mote: maybe no sample was taken at that time because it was not running on a core
    std::uint64_t LastTimestamp; // in nanoseconds
};
