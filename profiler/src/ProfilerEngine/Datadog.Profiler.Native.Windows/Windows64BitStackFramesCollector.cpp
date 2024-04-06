// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Windows64BitStackFramesCollector.h"

#ifdef BIT64

#include <cinttypes>
#include <winnt.h>

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "StackSamplerLoopManager.h"
#include "StackSnapshotResultBuffer.h"
#include "OsSpecificApi.h"

#endif // matches the '#ifdef BIT64' above

// ----------- Methods shared between the 64 implementation and the no-op stub used for 32 bit builds: -----------
void Windows64BitStackFramesCollector::SetOutputHrToLastError(uint32_t* pOutputHrCode)
{
    if (nullptr != pOutputHrCode)
    {
        DWORD errorCode = GetLastError();
        *pOutputHrCode = HRESULT_FROM_WIN32(errorCode);

        // If something went wrong with getting error code, ensure to return a generic failure.
        if (SUCCEEDED(*pOutputHrCode))
        {
            *pOutputHrCode = E_FAIL;
        }
    }
}

void Windows64BitStackFramesCollector::SetOutputHr(HRESULT value, uint32_t* pOutputHrCode)
{
    if (nullptr != pOutputHrCode)
    {
        *pOutputHrCode = value;
    }
}

// ----------- 64 bit specific implementation: -----------
#ifdef BIT64

constexpr THREADINFOCLASS ThreadInfoClass_ThreadBasicInformation = static_cast<THREADINFOCLASS>(0x0);

typedef struct _THREAD_BASIC_INFORMATION
{
    NTSTATUS ExitStatus;
    PVOID TebBaseAddress;
    CLIENT_ID ClientId;
    KAFFINITY AffinityMask;
    KPRIORITY Priority;
    KPRIORITY BasePriority;
} THREAD_BASIC_INFORMATION, *PTHREAD_BASIC_INFORMATION;

template <typename... Args>
void UnsafeLogErrorIfEnabledDuringStackwalk(const Args... args)
{
    if (LogDuringStackSampling_Unsafe)
    {
        Log::Error<Args...>(args...);
    }
}

template <typename... Args>
void UnsafeLogDebugIfEnabledDuringStackwalk(const Args... args)
{
    if (LogDuringStackSampling_Unsafe)
    {
        Log::Debug<Args...>(args...);
    }
}

Windows64BitStackFramesCollector::NtQueryInformationThreadDelegate_t Windows64BitStackFramesCollector::s_ntQueryInformationThreadDelegate = nullptr;

Windows64BitStackFramesCollector::Windows64BitStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo, IConfiguration const* configuration, CallstackProvider* callstackProvider) :
    StackFramesCollectorBase(configuration, callstackProvider),
    _pCorProfilerInfo(_pCorProfilerInfo)
{
    _pCorProfilerInfo->AddRef();
}

Windows64BitStackFramesCollector::~Windows64BitStackFramesCollector()
{
    _pCorProfilerInfo->Release();
}

bool Windows64BitStackFramesCollector::ValidatePointerInStack(DWORD64 pointerValue,
                                                              DWORD64 stackLimit,
                                                              DWORD64 stackBase,
                                                              const char* pointerMoniker)
{
    assert(nullptr != pointerMoniker);

    // Validate that the establisherFrame 8-byte aligned:
    if (0 != (0x7 & pointerValue))
    {
        UnsafeLogErrorIfEnabledDuringStackwalk("Windows64BitStackFramesCollector::CollectStackSample: ",
                                               pointerMoniker, " is not 8-byte aligned (0x", std::hex, pointerValue, std::dec, ").");
        return false;
    }

    // Vaidate stack limits. Attention! This may not apply to kernel frames / DPC stacks (http://www.nynaeve.net/?p=106):
    if ((stackLimit != 0 || stackBase != 0) && (pointerValue < stackLimit || stackBase <= pointerValue))
    {
        // Remember that stack grows downwards, i.e. stackLimit <= stackBase.
        UnsafeLogErrorIfEnabledDuringStackwalk("Windows64BitStackFramesCollector::CollectStackSample: ",
                                               pointerMoniker, " is outside the stack bounds (", std::hex,
                                               "stackLimit=0x", stackLimit,
                                               ", stackBase=0x", stackBase,
                                               ", ", pointerMoniker, "=0x", pointerValue,
                                               std::dec, ").");
        return false;
    }

    return true;
}

bool Windows64BitStackFramesCollector::TryGetThreadStackBoundaries(HANDLE threadHandle, DWORD64* pStackLimit, DWORD64* pStackBase)
{
    *pStackLimit = *pStackBase = 0;
    NtQueryInformationThreadDelegate_t ntQueryInformationThreadDelegate = s_ntQueryInformationThreadDelegate;
    if (nullptr == ntQueryInformationThreadDelegate)
    {
        HMODULE moduleHandle = GetModuleHandle(WStr("ntdll.dll"));
        if (NULL == moduleHandle)
        {
            moduleHandle = LoadLibrary(WStr("ntdll.dll"));
        }

        if (NULL != moduleHandle)
        {
            ntQueryInformationThreadDelegate = reinterpret_cast<NtQueryInformationThreadDelegate_t>(GetProcAddress(moduleHandle, "NtQueryInformationThread"));
            s_ntQueryInformationThreadDelegate = ntQueryInformationThreadDelegate;
        }
    }
    if (nullptr == ntQueryInformationThreadDelegate)
    {
        return false;
    }

    THREAD_BASIC_INFORMATION threadBasicInfo;
    ULONG threadInfoResultSize;
    bool hasThreadInfo = (0 == ntQueryInformationThreadDelegate(threadHandle,
                                                                ThreadInfoClass_ThreadBasicInformation,
                                                                &threadBasicInfo,
                                                                sizeof(THREAD_BASIC_INFORMATION),
                                                                &threadInfoResultSize));
    hasThreadInfo = hasThreadInfo && (threadInfoResultSize <= sizeof(THREAD_BASIC_INFORMATION));

    if (hasThreadInfo)
    {
        NT_TIB* pThreadTib = static_cast<NT_TIB*>(threadBasicInfo.TebBaseAddress);
        if (nullptr != pThreadTib)
        {
            *pStackBase = reinterpret_cast<DWORD64>(pThreadTib->StackBase);
            *pStackLimit = reinterpret_cast<DWORD64>(pThreadTib->StackLimit);
            return true;
        }
    }

    return false;
}

BOOL GetThreadInfo(ManagedThreadInfo* pThreadInfo, CONTEXT& context, HANDLE& handle)
{
    if (pThreadInfo == nullptr)
    {
        ::RtlCaptureContext(&context);
        handle = ::GetCurrentThread();
        return TRUE;
    }

    handle = pThreadInfo->GetOsThreadHandle();
    return ::GetThreadContext(handle, &context);
}

StackSnapshotResultBuffer* Windows64BitStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                              uint32_t* pHR,
                                                                                              bool selfCollect)
{
    // Collect data for TraceContext Tracking:
    bool traceContextDataCollected = this->TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();

    // Now walk the stack:
    CONTEXT context;
    context.ContextFlags = CONTEXT_FULL;
    HANDLE handle;
    BOOL hasInfo = GetThreadInfo(selfCollect ? nullptr : pThreadInfo, context, handle);

    if (!hasInfo)
    {
        SetOutputHrToLastError(pHR);
        return this->GetStackSnapshotResult();
    }

    // Get thread stack limits:
    DWORD64 stackLimit = 0;
    DWORD64 stackBase = 0;
    TryGetThreadStackBoundaries(handle, &stackLimit, &stackBase);

    uint64_t imageBaseAddress = 0;
    UNWIND_HISTORY_TABLE historyTable;
    ::RtlZeroMemory(&historyTable, sizeof(UNWIND_HISTORY_TABLE));
    historyTable.Search = TRUE;

    void* pHandlerData = nullptr;
    DWORD64 establisherFrame = 0;
    const PKNONVOLATILE_CONTEXT_POINTERS pNonVolatileContextPtrsIsNull = nullptr;
    RUNTIME_FUNCTION* pFunctionTableEntry;

    do
    {
        if (!this->AddFrame(context.Rip))
        {
            SetOutputHr(S_FALSE, pHR);
            return this->GetStackSnapshotResult();
        }

        __try
        {
            // Sometimes, we could hit an access violation, so catch it and just return.
            // We want to prevent this from killing the application
            pFunctionTableEntry = ::RtlLookupFunctionEntry(context.Rip, &imageBaseAddress, &historyTable);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            AddFakeFrame();

            SetOutputHr(E_ABORT, pHR);
            return this->GetStackSnapshotResult();
        }

        // RtlLookupFunctionEntry() may try to acquire global locks. The StackSamplerLoopManager should detect it and resume the
        // target thread, which will eventually allow the lookup to complete. In such case, the stack is invalid. Give up:
        if (this->IsCurrentCollectionAbortRequested())
        {
            this->AddFakeFrame();

            SetOutputHr(E_ABORT, pHR);
            return this->GetStackSnapshotResult();
        }

        if (nullptr == pFunctionTableEntry)
        {
            // So, we have a leaf function on the top of the stack. The calling convention rules imply:
            //     a) No RUNTIME_FUNCTION entry => Leaf function => this function does not modify stack
            //     b) RSP points to the top of the stack and
            //        the address it points to contains the return pointer from this leaf function.
            // So, we unwind one frame manually:
            //     1) Perform a virtual return:
            //         - The value at the top of the stack is the return address of this frame.
            //           That value needs to be written to the virtual instruction pointer register (RIP)
            //         - To access that value, we dereference the virtual stack register RSP,
            //           which contains the address of the logical top of the stack
            //     2) Perform a virtual stack pop to account for the value we just used from the stack:
            //         - Remember that x64 stack grows physically downwards.
            //         - So, add 8 bytes (=64 bits = sizeof(pointer)) to the virtual stack register RSP
            //
            __try
            {
                // FIX: For a customer using the SentinelOne solution, it was not possible to walk the stack
                //      of a thread so RSP was not valid
                context.Rip = *reinterpret_cast<uint64_t*>(context.Rsp);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                AddFakeFrame();

                SetOutputHr(E_ABORT, pHR);
                return this->GetStackSnapshotResult();
            }

            context.Rsp += 8;
        }
        else
        {
            // So, pFunctionTableEntry is not NULL. Unwind one frame.
            __try
            {
                // Sometimes, we could hit an access violation, so catch it and jus return.
                // We want to prevent this from killing the main application
                // Maybe cause by an incomplete context.
                ::RtlVirtualUnwind(UNW_FLAG_NHANDLER,
                                   imageBaseAddress,
                                   context.Rip,
                                   pFunctionTableEntry,
                                   &context,
                                   &pHandlerData,
                                   &establisherFrame,
                                   pNonVolatileContextPtrsIsNull);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                AddFakeFrame();

                SetOutputHr(E_ABORT, pHR);
                return this->GetStackSnapshotResult();
            }

            // RtlVirtualUnwind() may try to acquire global locks. The StackSamplerLoopManager should detect it and resume the
            // target thread, which will eventually allow the unwind to complete. In such case, the stack is invalid. Give up:
            if (this->IsCurrentCollectionAbortRequested())
            {
                this->AddFakeFrame();

                SetOutputHr(E_ABORT, pHR);
                return this->GetStackSnapshotResult();
            }

            // Sanity checks:
            if (!ValidatePointerInStack(establisherFrame, stackLimit, stackBase, "establisherFrame"))
            {
                SetOutputHr(ERROR_BAD_STACK, pHR);
                return this->GetStackSnapshotResult();
            }
        }

        if (!ValidatePointerInStack(context.Rsp, stackLimit, stackBase, "context.Rsp"))
        {
            SetOutputHr(ERROR_BAD_STACK, pHR);
            return this->GetStackSnapshotResult();
        }

    } while (context.Rip != 0);

    SetOutputHr(S_OK, pHR);
    return this->GetStackSnapshotResult();
}

BOOL Windows64BitStackFramesCollector::EnsureThreadIsSuspended(HANDLE hThread)
{
    CONTEXT ctx;
    ctx.ContextFlags = CONTEXT_INTEGER;

    return ::GetThreadContext(hThread, &ctx);
}

bool Windows64BitStackFramesCollector::SuspendTargetThreadImplementation(ManagedThreadInfo* pThreadInfo,
                                                                         bool* pIsTargetThreadSuspended)
{
    // ! REMEMBER: Do NOT log once a thread is suspended. Logging may allocate; allocating may deadlock !

    HANDLE osThreadHandle = pThreadInfo->GetOsThreadHandle();
    DWORD suspendCount = ::SuspendThread(osThreadHandle);
    if (suspendCount == static_cast<DWORD>(-1))
    {
        // We wanted to suspend, but it resulted in error.
        // This can happen when the thread died after we called managedThreads->LoopNext().
        // Give up.
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Info("Windows64BitStackFramesCollector::SuspendTargetThreadImplementation() SuspendThread() returned -1.",
                  " CLR tid=0x", std::hex, pThreadInfo->GetClrThreadId(), "; OS tid=", std::dec, pThreadInfo->GetOsThreadId(), " ", message);

        *pIsTargetThreadSuspended = false;
        return false;
    }

    // if suspendCount > 0, it means that we are not the only one who suspended the thread.
    // Might be a debugger or a different profiler. Assuming that we do correct suspension management
    // (and we have biger problems if we do not), this should be benign.

    // SuspendThread is asynchronous and requires GetThreadContext to be called.
    // https://devblogs.microsoft.com/oldnewthing/20150205-00/?p=44743
    if (EnsureThreadIsSuspended(pThreadInfo->GetOsThreadHandle()))
    {
        // We suspended the target thread successfully.
        *pIsTargetThreadSuspended = true;

        return true;
    }

    // The thread might have exited or being terminated
    // Same as in the CLR: https://sourcegraph.com/github.com/dotnet/runtime/-/blob/src/coreclr/vm/threadsuspend.cpp?L272
    ::ResumeThread(pThreadInfo->GetOsThreadHandle());

    *pIsTargetThreadSuspended = false;
    return false;
}

void Windows64BitStackFramesCollector::ResumeTargetThreadIfRequiredImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                  bool isTargetThreadSuspended,
                                                                                  uint32_t* pErrorCodeHR)
{
    if (!isTargetThreadSuspended)
    {
        SetOutputHr(S_OK, pErrorCodeHR);
        return;
    }

    HANDLE osThreadHandle = pThreadInfo->GetOsThreadHandle();
    DWORD suspendCountResm = ::ResumeThread(osThreadHandle);

    if (suspendCountResm == static_cast<DWORD>(-1))
    {

        SetOutputHrToLastError(pErrorCodeHR);
    }
    else
    {
        SetOutputHr(S_OK, pErrorCodeHR);
    }
}

#endif // '#ifdef BIT64'
