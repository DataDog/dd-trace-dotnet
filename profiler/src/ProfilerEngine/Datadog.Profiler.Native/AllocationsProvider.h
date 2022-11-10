// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "GroupSampler.h"
#include "IAllocationsListener.h"
#include "RawAllocationSample.h"

class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class ISampledAllocationsListener;


class AllocationsProvider
    :
    public CollectorBase<RawAllocationSample>,
    public IAllocationsListener
{
public:
    static std::vector<SampleValueType> SampleTypeDefinitions;

public:
    AllocationsProvider(
        uint32_t valueOffset,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        ISampledAllocationsListener* pListener);

    void OnAllocation(uint32_t allocationKind,
                      ClassID classId,
                      const WCHAR* TypeName,
                      uintptr_t Address,
                      uint64_t ObjectSize) override;

private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    ISampledAllocationsListener* _pListener = nullptr;
    GroupSampler<ClassID> _sampler;
    int32_t _sampleLimit;
};
