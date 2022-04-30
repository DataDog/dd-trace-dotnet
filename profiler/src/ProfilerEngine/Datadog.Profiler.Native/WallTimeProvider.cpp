// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IAppDomainStore.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "RawWallTimeSample.h"
#include "WallTimeProvider.h"


WallTimeProvider::WallTimeProvider(
    IConfiguration* pConfiguration,
    IFrameStore* pFrameStore,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore
    )
    :
    CollectorBase<RawWallTimeSample>(pConfiguration, pFrameStore, pAppDomainStore, pRuntimeIdStore)
{
}

const char* WallTimeProvider::GetName()
{
    return _serviceName;
}

void WallTimeProvider::OnTransformRawSample(const RawWallTimeSample& rawSample, Sample& sample)
{
    sample.AddValue(rawSample.Duration, SampleValue::WallTimeDuration);
}

