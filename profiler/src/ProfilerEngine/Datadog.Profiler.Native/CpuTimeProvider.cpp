// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CpuTimeProvider.h"

#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "RawCpuSample.h"
#include "RawSampleTransformer.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

std::vector<SampleValueType> CpuTimeProvider::SampleTypeDefinitions(
{
    {"cpu", "nanoseconds", -1},
    {"cpu-samples", "count", -1}}
);

CpuTimeProvider::CpuTimeProvider(
    SampleValueTypeProvider& valueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    shared::pmr::memory_resource* memoryResource
    )
    :
    CollectorBase<RawCpuSample>("CpuTimeProvider", valueTypeProvider.GetOrRegister(SampleTypeDefinitions), rawSampleTransformer, memoryResource)
{
}