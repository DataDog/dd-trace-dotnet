// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "WallTimeProvider.h"

#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "RawWallTimeSample.h"


std::vector<SampleValueType> WallTimeProvider::SampleTypeDefinitions(
    {
        {"wall", "nanoseconds"}
    }
    );

WallTimeProvider::WallTimeProvider(
    uint32_t valueOffset,
    IThreadsCpuManager* pThreadsCpuManager,
    IFrameStore* pFrameStore,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration
    )
    :
    CollectorBase<RawWallTimeSample>("WallTimeProvider", valueOffset, SampleTypeDefinitions.size(), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration)
{
}
