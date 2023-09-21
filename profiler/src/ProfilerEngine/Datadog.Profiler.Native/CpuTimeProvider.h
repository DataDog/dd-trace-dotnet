// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "RawCpuSample.h"

// forward declarations
class IConfiguration;
class IFrameStore;
class IAppDomainStore;
class IRuntimeIdStore;
class SampleValueTypeProvider;


class CpuTimeProvider
    :
    public CollectorBase<RawCpuSample> // accepts cputime samples
{
public:
    CpuTimeProvider(
        SampleValueTypeProvider& valueTypeProvider,
        IThreadsCpuManager* pThreadsCpuManager,
        IFrameStore* pFrameStore,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration
        );

private:
    static std::vector<SampleValueType> SampleTypeDefinitions;
};
