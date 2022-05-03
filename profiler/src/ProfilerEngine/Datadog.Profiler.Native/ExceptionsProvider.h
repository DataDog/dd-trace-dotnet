#pragma once

#include "CollectorBase.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "RawExceptionSample.h"
#include "cor.h"
#include "corprof.h"
#include "OsSpecificApi.h"
#include "StackSnapshotResultReusableBuffer.h"

class ExceptionsProvider
    : public CollectorBase<RawExceptionSample>
{
public:
    ExceptionsProvider(
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IConfiguration* pConfiguration,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore);

    bool OnModuleLoaded(ModuleID moduleId);
    bool OnExceptionThrown(ObjectID exception);

    const char* GetName() override;

    ~ExceptionsProvider() override;

protected:
    void OnTransformRawSample(const RawExceptionSample& rawSample, Sample& sample) override;

private:
    bool LoadExceptionMetadata();
    bool GetExceptionType(ClassID classId, std::string& exceptionType);

private:
    const char* _serviceName = "ExceptionsProvider";

    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    COR_FIELD_OFFSET _messageFieldOffset;
    ULONG _stringLengthOffset;
    ULONG _stringBufferOffset;
    ModuleID _mscorlibModuleId;
    ClassID _exceptionClassId;
    StackFramesCollectorBase* _pStackFramesCollector;
    bool _loggedMscorlibError;
    std::unordered_map<ClassID, std::string> _exceptionTypes;
    std::mutex _exceptionTypesLock;
};
