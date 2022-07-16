// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawCpuSample : public RawSample
{
public:
    inline void OnTransform(Sample& sample) const override
    {
        sample.AddValue(Duration * 1000000, SampleValue::CpuTimeDuration);
    }

    std::uint64_t Duration;  // in milliseconds
};
