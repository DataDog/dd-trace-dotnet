// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IAppDomainStore.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "RawCpuSample.h"
#include "CpuTimeProvider.h"

CpuTimeProvider::CpuTimeProvider(
    IConfiguration* pConfiguration,
    IFrameStore* pFrameStore,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore
    )
    :
    CollectorBase<RawCpuSample>(pConfiguration, pFrameStore, pAppDomainStore, pRuntimeIdStore)
{
}


const char* CpuTimeProvider::GetName()
{
    return _serviceName;
}

void CpuTimeProvider::OnTransformRawSample(const RawCpuSample& rawSample, Sample& sample)
{
    // from milliseconds to nanoseconds
    sample.AddValue(rawSample.Duration * 1000000, SampleValue::CpuTimeDuration);
}
