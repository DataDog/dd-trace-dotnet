// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "PInvoke.h"
#include "CorProfilerCallback.h"
#include "IClrLifetime.h"
#include "Log.h"
#include "ManagedThreadList.h"
#include "ProfilerEngineStatus.h"
#include "StackFrameCodeKind.h"
#include "StackSnapshotsBufferManager.h"
#include "SymbolsResolver.h"
#include "ThreadsCpuManager.h"

#include "shared/src/native-src/loader.h"

// There is a race condition when dealing with CLR shutdown:
// it could happen AFTER the CorProfilerCallback::GetClrLifetime()->IsRunning() check
// In that case, exceptions could be thrown when ICorProfilerInfo methods are called
// These macros deal with that scenario by catching exceptions
//
#define PROTECT_ENTER \
    try               \
    {
#define PROTECT_LEAVE(name)                                \
    }                                                      \
    catch (...)                                            \
    {                                                      \
        Log::Info(name, " is called AFTER CLR shutdown."); \
    }                                                      \
    return FALSE;

//
// P/Invoke calls are "protected" against being called AFTER ICorProfilerCallback::Shutdown
// Error message is added to the log as Info and not Error so the CI is not blocked when
// this happens
//

extern "C" void __stdcall ThreadsCpuManager_Map(std::uint32_t threadId, const WCHAR* pName)
{
    ThreadsCpuManager::GetSingletonInstance()->Map(threadId, pName);
}

extern "C" BOOL __stdcall TryCompleteCurrentWriteSegment(bool* pSuccess)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        Log::Info("TryCompleteCurrentWriteSegment is called AFTER CLR shutdown");
        return FALSE;
    }

    PROTECT_ENTER
    *pSuccess = StackSnapshotsBufferManager::GetSingletonInstance()->TryCompleteCurrentWriteSegment();
    return TRUE;
    PROTECT_LEAVE("TryCompleteCurrentWriteSegment")
}

extern "C" BOOL __stdcall TryMakeSegmentAvailableForWrite(void* segment, bool* pIsReleased)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        Log::Info("TryMakeSegmentAvailableForWrite is called AFTER CLR shutdown");
        return FALSE;
    }

    PROTECT_ENTER
    *pIsReleased = StackSnapshotsBufferManager::GetSingletonInstance()->TryMakeSegmentAvailableForWrite(static_cast<StackSnapshotsBufferSegment*>(segment));
    return TRUE;
    PROTECT_LEAVE("TryMakeSegmentAvailableForWrite")
}

extern "C" BOOL __stdcall DebugDumpAllSnapshots(void* stackSnapshotsBufferSegmentPtr)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        Log::Info("DebugDumpAllSnapshots is called AFTER CLR shutdown");
        return FALSE;
    }

    if (stackSnapshotsBufferSegmentPtr == nullptr)
    {
        return TRUE;
    }

    PROTECT_ENTER
    StackSnapshotsBufferSegment* pSegment = static_cast<StackSnapshotsBufferSegment*>(stackSnapshotsBufferSegmentPtr);
    pSegment->DebugDumpAllSnapshots();
    return TRUE;
    PROTECT_LEAVE("DebugDumpAllSnapshots")
}

extern "C" BOOL __stdcall TryResolveStackFrameSymbols(StackFrameCodeKind frameCodeKind,
                                                      std::uint64_t frameInfoCode,
                                                      const WCHAR** ppFunctionName,
                                                      const WCHAR** ppContainingTypeName,
                                                      const WCHAR** ppContainingAssemblyName)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        Log::Info("TryResolveStackFrameSymbols is called AFTER CLR shutdown");
        return FALSE;
    }

    PROTECT_ENTER
    StackSnapshotResultFrameInfo snapshot(frameCodeKind, frameInfoCode);
    StackFrameInfo* resolvedFrame;

    if (!SymbolsResolver::GetSingletonInstance()->ResolveStackFrameSymbols(snapshot, &resolvedFrame, true))
        return TRUE;

    if (ppFunctionName != nullptr)
    {
        *ppFunctionName = resolvedFrame->GetFunctionName()->c_str();
    }

    if (ppContainingTypeName != nullptr)
    {
        *ppContainingTypeName = resolvedFrame->GetContainingTypeName()->c_str();
    }

    if (ppContainingAssemblyName != nullptr)
    {
        *ppContainingAssemblyName = resolvedFrame->GetContainingAssemblyName()->c_str();
    }

    return TRUE;
    PROTECT_LEAVE("TryResolveStackFrameSymbols")
}

extern "C" BOOL __stdcall TryResolveAppDomainInfoSymbols(std::uint64_t profilerAppDomainId,
                                                         std::uint32_t appDomainNameBuffSize,
                                                         std::uint32_t* pActualAppDomainNameLen,
                                                         WCHAR* pAppDomainNameBuff,
                                                         std::uint64_t* pAppDomainProcessId,
                                                         bool* pSuccess)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        Log::Info("TryResolveAppDomainInfoSymbols is called AFTER CLR shutdown");
        return FALSE;
    }

    AppDomainID adId = static_cast<AppDomainID>(profilerAppDomainId);

    PROTECT_ENTER
    *pSuccess =
        SymbolsResolver::GetSingletonInstance()->ResolveAppDomainInfoSymbols(adId,
                                                                             appDomainNameBuffSize,
                                                                             pActualAppDomainNameLen,
                                                                             pAppDomainNameBuff,
                                                                             pAppDomainProcessId,
                                                                             /*offloadToWorkerThread:*/ true);
    return TRUE;
    PROTECT_LEAVE("TryResolveAppDomainInfoSymbols")
}

extern "C" BOOL __stdcall TryGetThreadInfo(const std::uint32_t profilerThreadInfoId,
                                           std::int64_t* pClrThreadId,
                                           std::uint32_t* pOsThreadId,
                                           void** pOsThreadHandle,
                                           WCHAR* pThreadNameBuff,
                                           const std::uint32_t threadNameBuffSize,
                                           std::uint32_t* pActualThreadNameLen,
                                           bool* pSuccess)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
        return FALSE;

    PROTECT_ENTER
    DWORD pOsThreadIdBuf;

    // This is to make 32 bits targets compile.
    // TryGetThreadInfo() puts a value that is (ThreadID*)-bits wide into the address pointed to by pClrThreadId.
    ThreadID* pClrThreadIdPtr = static_cast<ThreadID*>(static_cast<void*>(pClrThreadId));

    *pSuccess = ManagedThreadList::GetSingletonInstance()->TryGetThreadInfo(profilerThreadInfoId,
                                                                            pClrThreadIdPtr,
                                                                            &pOsThreadIdBuf,
                                                                            pOsThreadHandle,
                                                                            pThreadNameBuff,
                                                                            threadNameBuffSize,
                                                                            pActualThreadNameLen);
    *pOsThreadId = static_cast<std::uint32_t>(pOsThreadIdBuf);

    return TRUE;
    PROTECT_LEAVE("TryGetThreadInfo")
}

extern "C" BOOL __stdcall GetAssemblyAndSymbolsBytes(void** ppAssemblyArray, int* pAssemblySize, void** ppSymbolsArray, int* pSymbolsSize, WCHAR* moduleName)
{
    return shared::Loader::GetSingletonInstance()->GetAssemblyAndSymbolsBytes(ppAssemblyArray, pAssemblySize, ppSymbolsArray, pSymbolsSize, moduleName);
}

extern "C" HRESULT _stdcall TraceContextTracking_GetInfoFieldPointersForCurrentThread(const bool** ppIsNativeProfilerEngineActiveFlag,
                                                                                      std::uint64_t** ppCurrentTraceId,
                                                                                      std::uint64_t** ppCurrentSpanId)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        return E_UNEXPECTED;
    }
    // TODO: discuss with Tracer how to handle the Shutdown case --> returning E_UNEXPECTED?

    const bool* pIsNativeProfilerEngineActiveFlag = ProfilerEngineStatus::GetReadPtrIsProfilerEngineActive();
    if (ppIsNativeProfilerEngineActiveFlag != nullptr)
    {
        *ppIsNativeProfilerEngineActiveFlag = pIsNativeProfilerEngineActiveFlag;
    }

    // If the native engine is not active, do not try get pointers to thread specific data.
    if (*pIsNativeProfilerEngineActiveFlag == false)
    {
        if (ppCurrentTraceId != nullptr)
        {
            *ppCurrentTraceId = nullptr;
        }

        if (ppCurrentSpanId != nullptr)
        {
            *ppCurrentSpanId = nullptr;
        }

        // TODO: should not return S_OK if the runtime is shutdown!!!
        return S_OK;
    }

    // Engine is active. Get info for current thread.
    ManagedThreadInfo* pCurrentThreadInfo;
    HRESULT hr = ManagedThreadList::GetSingletonInstance()->TryGetCurrentThreadInfo(&pCurrentThreadInfo);
    if (FAILED(hr))
    {
        // There was an error looking up the current thread info:
        return hr;
    }

    if (S_FALSE == hr)
    {
        // There was no error looking up the current thread, but we are not tracking any info for this thread:
        return E_FAIL;
    }

    // Get pointers to the relevant fields within the thread info data structure.
    pCurrentThreadInfo->GetTraceContextInfoFieldPointers(ppCurrentTraceId, ppCurrentSpanId);

    return S_OK;
}

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------
#define CALLBACK_REG_SCOPE ManagedCallbackRegistry::EnqueueStackSnapshotBufferSegmentForExport

std::mutex CALLBACK_REG_SCOPE::_invocationLock;
CALLBACK_REG_SCOPE::Delegate_t CALLBACK_REG_SCOPE::_pCallback = nullptr;

extern "C" void* __stdcall ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set(void* pCallback)
{
    CALLBACK_REG_SCOPE::Delegate_t pPrevExistingCallback = CALLBACK_REG_SCOPE::Set(reinterpret_cast<CALLBACK_REG_SCOPE::Delegate_t>(pCallback));
    return reinterpret_cast<void*>(pPrevExistingCallback);
}

CALLBACK_REG_SCOPE::Delegate_t CALLBACK_REG_SCOPE::Set(CALLBACK_REG_SCOPE::Delegate_t pCallback)
{
    std::lock_guard<std::mutex> lock(_invocationLock);

    CALLBACK_REG_SCOPE::Delegate_t pPrevExistingCallback = _pCallback;
    if (pPrevExistingCallback != nullptr && pCallback != nullptr)
    {
        // Resetting the callback to NULL is normal for the managed engine shutdown procedure.
        // But if the callback is already configured and we are configuring it again to a non-NULL value, then something is wrong.
        Log::Error("The Callback Delegate for the reverse-PInvoke function \"EnqueueStackSnapshotBufferSegmentForExport\" is being set, although that callback is already configured."
                   " Please report this log entry to Datadog and provide the complete log set for investigation."
                   );
    }

    _pCallback = pCallback;
    return pPrevExistingCallback;
}

bool CALLBACK_REG_SCOPE::TryInvoke(void* segmentNativeObjectPtr,
                                   void* segmentMemory,
                                   std::uint32_t segmentByteCount,
                                   std::uint32_t segmentSnapshotCount,
                                   std::uint64_t segmentUnixTimeUtcRangeStart,
                                   std::uint64_t segmentUnixTimeUtcRangeEnd,
                                   HRESULT* result)
{
    std::lock_guard<std::mutex> lock(_invocationLock);

    CALLBACK_REG_SCOPE::Delegate_t pCallback = _pCallback;
    if (pCallback == nullptr)
    {
        return false;
    }

    HRESULT r = pCallback(segmentNativeObjectPtr,
                          segmentMemory,
                          segmentByteCount,
                          segmentSnapshotCount,
                          segmentUnixTimeUtcRangeStart,
                          segmentUnixTimeUtcRangeEnd);

    if (result != nullptr)
    {
        *result = r;
    }

    return true;
}

#undef CALLBACK_REG_SCOPE

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------
#define CALLBACK_REG_SCOPE ManagedCallbackRegistry::TryShutdownCurrentManagedProfilerEngine

std::mutex CALLBACK_REG_SCOPE::_invocationLock;
CALLBACK_REG_SCOPE::Delegate_t CALLBACK_REG_SCOPE::_pCallback = nullptr;

extern "C" void* __stdcall ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set(void* pCallback)
{
    CALLBACK_REG_SCOPE::Delegate_t pPrevExistingCallback = CALLBACK_REG_SCOPE::Set(reinterpret_cast<CALLBACK_REG_SCOPE::Delegate_t>(pCallback));
    return reinterpret_cast<void*>(pPrevExistingCallback);
}

CALLBACK_REG_SCOPE::Delegate_t CALLBACK_REG_SCOPE::Set(CALLBACK_REG_SCOPE::Delegate_t pCallback)
{
    std::lock_guard<std::mutex> lock(_invocationLock);

    CALLBACK_REG_SCOPE::Delegate_t pPrevExistingCallback = _pCallback;
    if (pPrevExistingCallback != nullptr && pCallback != nullptr)
    {
        // Resetting the callback to NULL is normal for the managed engine shut-down procedure.
        // But if the callback is already configured and we are configuring it again to a non-NULL value, then somethng is likely wrong.
        Log::Error("The Callback Delegate for the reverse-PInvoke function \"TryShutdownCurrentManagedProfilerEngine\" is being set, although that callback is already configured."
                   " Please report this log entry to Datadog and provide the complete log set for investigation."
                   );
    }

    _pCallback = pCallback;
    return pPrevExistingCallback;
}

bool CALLBACK_REG_SCOPE::TryInvoke(bool* pResult)
{
    std::lock_guard<std::mutex> lock(_invocationLock);

    CALLBACK_REG_SCOPE::Delegate_t pCallback = _pCallback;
    if (pCallback == nullptr)
    {
        return false;
    }
    bool r = pCallback();

    if (pResult != nullptr)
    {
        *pResult = r;
    }

    return true;
}

#undef CALLBACK_REG_SCOPE

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------
#define CALLBACK_REG_SCOPE ManagedCallbackRegistry::SetCurrentManagedThreadName

std::mutex CALLBACK_REG_SCOPE::_invocationLock;
CALLBACK_REG_SCOPE::Delegate_t CALLBACK_REG_SCOPE::_pCallback = nullptr;

extern "C" void* __stdcall ManagedCallbackRegistry_SetCurrentManagedThreadName_Set(void* pCallback)
{
    CALLBACK_REG_SCOPE::Delegate_t pPrevExistingCallback = CALLBACK_REG_SCOPE::Set(reinterpret_cast<CALLBACK_REG_SCOPE::Delegate_t>(pCallback));
    return reinterpret_cast<void*>(pPrevExistingCallback);
}

CALLBACK_REG_SCOPE::Delegate_t CALLBACK_REG_SCOPE::Set(CALLBACK_REG_SCOPE::Delegate_t pCallback)
{
    std::lock_guard<std::mutex> lock(_invocationLock);

    CALLBACK_REG_SCOPE::Delegate_t pPrevExistingCallback = _pCallback;
    if (pPrevExistingCallback != nullptr)
    {
        // None that the following log line and its semantics are not the same as other similar log events in the ManagedCallbackRegistry.
        Log::Info("The Callback Delegate for the reverse-PInvoke function \"SetCurrentManagedThreadName\" is being set, although that callback is already configured."
                  " This callback's lifetime is not bound to the lifetime of a Managed Profiler Engine instance."
                  " Typically, this function is expected to be called not more than once per static type instance of the"
                  " static class Datadog.Profiler.ThreadUtil, i.e. not more than once per AppDomain."
                  " Loading ThreadUtil into multiple AppDomains is benign, but currently not expected. This may (or may not) indicate an issue and should be investigated."
                  " Please report this log entry to Datadog and provide the complete log set for investigation."
                  );
    }

    _pCallback = pCallback;
    return pPrevExistingCallback;
}

bool CALLBACK_REG_SCOPE::TryInvoke(const char* pThreadNameCharArr, HRESULT* pResult)
{
    std::lock_guard<std::mutex> lock(_invocationLock);

    CALLBACK_REG_SCOPE::Delegate_t pCallback = _pCallback;
    if (pCallback == nullptr)
    {
        return false;
    }

    HRESULT r = pCallback(const_cast<void*>(static_cast<const void*>(pThreadNameCharArr)));

    if (pResult != nullptr)
    {
        *pResult = r;
    }

    return true;
}

#undef CALLBACK_REG_SCOPE
