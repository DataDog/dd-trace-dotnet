// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "WallTimeProvider.h"

#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "RawWallTimeSample.h"

class SampleValueTypeProvider;

std::vector<SampleValueType> WallTimeProvider::SampleTypeDefinitions(
    {
        {"wall", "nanoseconds", -1}
    }
    );

WallTimeProvider::WallTimeProvider(
    SampleValueTypeProvider& sampleValueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    shared::pmr::memory_resource* memoryResource
    )
    :
    CollectorBase<RawWallTimeSample>("WallTimeProvider", sampleValueTypeProvider.GetOrRegister(SampleTypeDefinitions), rawSampleTransformer, memoryResource)
{
}
