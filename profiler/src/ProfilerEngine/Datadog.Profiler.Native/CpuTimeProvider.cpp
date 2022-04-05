// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IAppDomainStore.h"
#include "RawCpuSample.h"
#include "RawCpuSample.h"
#include "CpuTimeProvider.h"

CpuTimeProvider::CpuTimeProvider(IConfiguration* pConfiguration, IFrameStore* pFrameStore, IAppDomainStore* pAssemblyStore)
    :
    ProviderBase<RawCpuSample>(pConfiguration, pFrameStore, pAssemblyStore)
{
}


const char* CpuTimeProvider::GetName()
{
    return _serviceName;
}

void CpuTimeProvider::OnTransformRawSample(const RawCpuSample& rawSample, Sample& sample)
{
    sample.AddValue(rawSample.Duration, SampleValue::CpuTimeDuration);
}
