// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "StackFramesCollectorBase.h"

class StackSnapshotResultReusableBuffer;
struct ManagedThreadInfo;

class Windows32BitStackFramesCollector : public StackFramesCollectorBase
{
public:
    explicit Windows32BitStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo);
    ~Windows32BitStackFramesCollector() override;

    void OnDeadlock() override;

protected:
    StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                uint32_t* pHR) override;

private:
    ICorProfilerInfo4* const _pCorProfilerInfo;

private:
    friend HRESULT STDMETHODCALLTYPE StackSnapshotCallbackHandlerImpl(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData);

    struct StackSnapshotCallbackClientData
    {
        StackSnapshotCallbackClientData(ManagedThreadInfo* pThread, Windows32BitStackFramesCollector* pCollector) :
            PtrthreadInfo(pThread), PtrStackFramesCollector(pCollector)
        {
        }
        ManagedThreadInfo const* const PtrthreadInfo;
        Windows32BitStackFramesCollector* const PtrStackFramesCollector;
    };
};
