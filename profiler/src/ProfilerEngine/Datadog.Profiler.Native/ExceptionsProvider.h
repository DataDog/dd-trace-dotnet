// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "IUpscaleProvider.h"
#include "RawExceptionSample.h"
#include "cor.h"
#include "corprof.h"
#include "GroupSampler.h"
#include "OsSpecificApi.h"
#include "StackSnapshotResultBuffer.h"
#include "MetricsRegistry.h"
#include "CounterMetric.h"
#include "IUpscaleProvider.h"

class IConfiguration;

class ExceptionsProvider :
    public CollectorBase<RawExceptionSample>,
    public IUpscaleProvider
{
public:
    static std::vector<SampleValueType> SampleTypeDefinitions;

public:
    ExceptionsProvider(
        uint32_t valueOffset,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IConfiguration* pConfiguration,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        MetricsRegistry& metricsRegistry);

    bool OnModuleLoaded(ModuleID moduleId);
    bool OnExceptionThrown(ObjectID thrownObjectId);

    UpscalingInfo GetInfo() override;

private:
    struct ExceptionBucket
    {
        std::string Name;
        uint64_t Count;
    };

private:
    bool LoadExceptionMetadata();
    bool GetExceptionType(ClassID classId, std::string& exceptionType);


private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    COR_FIELD_OFFSET _messageFieldOffset;
    ULONG _stringLengthOffset;
    ULONG _stringBufferOffset;
    ModuleID _mscorlibModuleId;
    ClassID _exceptionClassId;
    bool _loggedMscorlibError;
    std::unordered_map<ClassID, std::string> _exceptionTypes;
    std::mutex _exceptionTypesLock;
    GroupSampler<std::string> _sampler;
    IConfiguration const* const _pConfiguration;
    std::shared_ptr<CounterMetric> _exceptionsCountMetric;
    std::shared_ptr<CounterMetric> _sampledExceptionsCountMetric;
};
