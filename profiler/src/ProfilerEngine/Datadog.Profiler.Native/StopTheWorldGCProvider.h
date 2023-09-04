// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "IGCSuspensionsListener.h"
#include "RawStopTheWorldSample.h"

class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class IConfiguration;
class SampleValueTypeProvider;

class StopTheWorldGCProvider
    : public CollectorBase<RawStopTheWorldSample>,
      public IGCSuspensionsListener
{
    // use the same sample type definition as the GarbageCollectorProvider

public:
    StopTheWorldGCProvider(
        SampleValueTypeProvider& valueTypeProvider,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration);

    // Inherited via IGCSuspensionsListener
    void OnSuspension(int32_t number, uint32_t generation, uint64_t pauseDuration, uint64_t timestamp) override;
};
