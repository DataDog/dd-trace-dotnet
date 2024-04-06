// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "Log.h"
#include "StackFramesCollectorBase.h"
#include <winternl.h>

class CallstackProvider;
class StackSnapshotResultReusableBuffer;
struct ManagedThreadInfo;
class IManagedThreadList;

class Windows64BitStackFramesCollector : public StackFramesCollectorBase
{
#ifdef BIT64

    // ----------- 64 bit specific implementation: -----------

public:
    explicit Windows64BitStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo, IConfiguration const* configuration, CallstackProvider* callstackProvider);
    ~Windows64BitStackFramesCollector() override;

    bool SuspendTargetThreadImplementation(ManagedThreadInfo* pThreadInfo,
                                           bool* pIsTargetThreadSuspended) override;

    void ResumeTargetThreadIfRequiredImplementation(ManagedThreadInfo* pThreadInfo, bool isTargetThreadSuspended, uint32_t* pErrorCodeHR) override;

    StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo, uint32_t* pHR, bool selfCollect) override;

private:
    typedef NTSTATUS(__stdcall* NtQueryInformationThreadDelegate_t)(HANDLE ThreadHandle, THREADINFOCLASS ThreadInformationClass, PVOID ThreadInformation, ULONG ThreadInformationLength, PULONG ReturnLength);

private:
    ICorProfilerInfo4* const _pCorProfilerInfo;

private:
    static bool ValidatePointerInStack(DWORD64 pointerValue, DWORD64 lowStackLimit, DWORD64 highStackLimit, const char* pointerMoniker);
    static bool TryGetThreadStackBoundaries(HANDLE threadHandle, DWORD64* pLowStackLimit, DWORD64* pHighStackLimit);
    static BOOL EnsureThreadIsSuspended(HANDLE hThread);

    static NtQueryInformationThreadDelegate_t s_ntQueryInformationThreadDelegate;
#else // #ifdef BIT64

    // ----------- 32 bit no-op stub implementation: -----------
    // (This collector is not meant for 32 bit builds. Use the 32 bit collector instead.)

public:
    Windows64BitStackFramesCollector(ICorProfilerInfo4* const _, IConfiguration const* configuration, CallstackProvider* callstackProvider) :
        StackFramesCollectorBase(configuration, callstackProvider)
    {
        Log::Error("Windows64BitStackFramesCollector used in a 32 bit build."
                   " This was not intended. Use Windows32BitStackFramesCollector instead.");
    }

    ~Windows64BitStackFramesCollector() override
    {
    }

#endif // #ifdef BIT64

private:
    // ----------- Methods shared between the 64 implementation and the no-op stub used for 32 bit builds: -----------

    static void SetOutputHrToLastError(uint32_t* pOutputHrCode);
    static void SetOutputHr(HRESULT value, uint32_t* pOutputHrCode);
};
