#pragma once

#include "cor.h"
#include "corprof.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"

class ExceptionsManager
{
public:
    explicit ExceptionsManager(ICorProfilerInfo4* pCorProfilerInfo, IManagedThreadList* pManagedThreadList, IFrameStore* pFrameStore);

public:
    void OnModuleLoaded(ModuleID moduleId);
    void OnExceptionThrown(ObjectID exception);

private:
    bool LoadExceptionMetadata();

private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    COR_FIELD_OFFSET _messageFieldOffset;
    ULONG _stringLengthOffset;
    ULONG _stringBufferOffset;
    ModuleID _mscorlibModuleId;
    ClassID _exceptionClassId;
};
