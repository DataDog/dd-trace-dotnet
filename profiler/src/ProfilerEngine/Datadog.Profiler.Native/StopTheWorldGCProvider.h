// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "IGCSuspensionsListener.h"
#include "RawStopTheWorldSample.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

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
        RawSampleTransformer* rawSampleTransformer,
        shared::pmr::memory_resource* memoryResource);

    // Inherited via IGCSuspensionsListener
    void OnSuspension(std::chrono::nanoseconds timestamp, int32_t number, uint32_t generation, std::chrono::nanoseconds pauseDuration) override;
};
