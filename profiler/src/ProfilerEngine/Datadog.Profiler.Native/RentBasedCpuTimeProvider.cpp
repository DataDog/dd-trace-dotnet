// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RentBasedCpuTimeProvider.h"

#include "CpuTimeProvider.h"

#include "RawCpuSample.h"
#include "RawSampleTransformer.h"

RentBasedCpuTimeProvider::RentBasedCpuTimeProvider(
    SampleValueTypeProvider& valueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    std::unique_ptr<RingBuffer> ringBuffer
    )
    :
    RentBasedCollector<RawCpuSample>("RentBasedCpuTimeProvider", valueTypeProvider.GetOrRegister(CpuTimeProvider::SampleTypeDefinitions), rawSampleTransformer, std::move(ringBuffer))
{
}