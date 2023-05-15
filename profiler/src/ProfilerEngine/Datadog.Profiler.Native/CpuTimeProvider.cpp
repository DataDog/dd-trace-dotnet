// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CpuTimeProvider.h"

#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"
#include "RawCpuSample.h"


std::vector<SampleValueType> CpuTimeProvider::SampleTypeDefinitions(
    {
        {"cpu", "nanoseconds"}
    }
    );


CpuTimeProvider::CpuTimeProvider(
    uint32_t valueOffset,
    IThreadsCpuManager* pThreadsCpuManager,
    IFrameStore* pFrameStore,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration
    )
    :
    CollectorBase<RawCpuSample>("CpuTimeProvider", valueOffset, SampleTypeDefinitions.size(), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration)
{
}