// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <vector>

#include "CollectorBase.h"
#include "IThreadLifetimeListener.h"
#include "RawThreadLifetimeSample.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

class IThreadsCpuManager;
class RawSampleTransformer;

class ThreadLifetimeProvider
    : public CollectorBase<RawThreadLifetimeSample>,
      public IThreadLifetimeListener
{
public:
    ThreadLifetimeProvider(
        SampleValueTypeProvider& valueTypeProvider,
        RawSampleTransformer* rawSampleTransformer,
        shared::pmr::memory_resource* memoryResource);

    // Inherited via IThreadLifetimeListener
    void OnThreadStart(std::shared_ptr<ManagedThreadInfo> threadInfo) override;
    void OnThreadStop(std::shared_ptr<ManagedThreadInfo> threadInfo) override;

private:
    RawThreadLifetimeSample CreateSample(std::shared_ptr<ManagedThreadInfo> pThreadInfo, ThreadEventKind kind);
    uint64_t GetCurrentTimestamp();
};
