// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"

class RawContentionSample : public RawSample
{
public:
    void OnTransform(Sample& sample, uint32_t valueOffset) const override
    {
        uint32_t contentionCountIndex = valueOffset;
        uint32_t contentionDurationIndex = valueOffset + 1;

        sample.AddValue(1, contentionCountIndex);
        sample.AddValue(static_cast<std::int64_t>(ContentionDuration), contentionDurationIndex);
    }

    double ContentionDuration;
};