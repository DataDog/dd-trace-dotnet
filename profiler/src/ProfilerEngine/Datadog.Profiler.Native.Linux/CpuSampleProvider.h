// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "MetricsRegistry.h"
#include "RawSampleCollectorBase.h"
#include "RawCpuSample.h"
#include "RingBuffer.h"

#include <memory>
#include <vector>

// forward declarations
class SampleValueTypeProvider;
class RawSampleTransformer;

class CpuSampleProvider
    :
    public RawSampleCollectorBase<RawCpuSample>
{
public:
    CpuSampleProvider(
        SampleValueTypeProvider& valueTypeProvider,
        RawSampleTransformer* rawSampleTransformer,
        RingBuffer* ringBuffer,
        MetricsRegistry& metricsRegistry);
};
