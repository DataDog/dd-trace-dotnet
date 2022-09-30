// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "CollectorBase.h"

#include "IContentionListener.h"
#include "RawContentionSample.h"
#include "GenericSampler.h"

class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;


class ContentionProvider : public CollectorBase<RawContentionSample>, public IContentionListener
{
public:
    ContentionProvider(
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration);

    void OnContention(double contentionDuration) override;

private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    GenericSampler _sampler;
    int32_t _contentionDurationThreshold;
    int32_t _sampleLimit;
};
