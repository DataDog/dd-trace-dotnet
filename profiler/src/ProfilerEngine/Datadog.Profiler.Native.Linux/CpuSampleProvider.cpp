// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CpuSampleProvider.h"

#include "CpuTimeProvider.h"

#include "RawCpuSample.h"
#include "RawSampleTransformer.h"

CpuSampleProvider::CpuSampleProvider(
    SampleValueTypeProvider& valueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    RingBuffer* ringBuffer,
    MetricsRegistry& metricsRegistry
    )
    :
    RawSampleCollectorBase<RawCpuSample>("CpuSampleProvider", valueTypeProvider.GetOrRegister(CpuTimeProvider::SampleTypeDefinitions), rawSampleTransformer, ringBuffer, metricsRegistry)
{
}