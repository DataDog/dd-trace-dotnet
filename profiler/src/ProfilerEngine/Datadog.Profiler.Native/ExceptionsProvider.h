// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "RawExceptionSample.h"
#include "cor.h"
#include "corprof.h"
#include "ExceptionSampler.h"
#include "OsSpecificApi.h"
#include "StackSnapshotResultBuffer.h"

class ExceptionsProvider
    : public CollectorBase<RawExceptionSample>
{
public:
    ExceptionsProvider(
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IConfiguration* pConfiguration,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore);

    bool OnModuleLoaded(ModuleID moduleId);
    bool OnExceptionThrown(ObjectID exception);

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
    ExceptionSampler _sampler;
};
