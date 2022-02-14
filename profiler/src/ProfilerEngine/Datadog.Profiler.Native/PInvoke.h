// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "unknwn.h"
#include <cstdint>
#include <mutex>
#include <winerror.h>

#include "StackFrameCodeKind.h"

/*
   TL;DR When returning a boolean value to the managed part, we must use a C BOOL type instead of C++ bool type.

   Boolean type has different size/representation in C (4 bytes), C++ (1 byte) and C# (1 byte).

   For example:
   [DllImport(dllName: NativeProfilerEngineLibName_x86, EntryPoint = "StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment",
              CallingConvention = CallingConvention.StdCall)]
   private static extern bool StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment_x86();

   If not specified otherwise, the marshaller will consider the boolean type as a Win32 BOOL type (4 bytes). It will read the register RAX/EAX
   and convert its content to the appropriate C# bool value (false == 0, true otherwise).

   On the native side:
   extern "C" bool __stdcall StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment();

   In this case, the function return a C++ bool value (1 byte). So only the AL register will be set as the return value. However, he AL
   is lowest byte of the RAX/EAX register.
   This means that the RAX/EAX register will have only its first byte changed: the other bytes will remain unchanged with probably
   garbage definitely not 0.

   In this case, if StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment returns false, the managed part may see the return value
   as true.

 */

extern "C" BOOL __stdcall TryCompleteCurrentWriteSegment(bool* pSuccess);
extern "C" BOOL __stdcall TryMakeSegmentAvailableForWrite(void* segment, bool* pIsReleased);

extern "C" BOOL __stdcall DebugDumpAllSnapshots(void* stackSnapshotsBufferSegmentPtr);

extern "C" BOOL __stdcall TryResolveStackFrameSymbols(StackFrameCodeKind frameCodeKind,
                                                      std::uint64_t frameInfoCode,
                                                      const WCHAR** ppFunctionName,
                                                      const WCHAR** ppContainingTypeName,
                                                      const WCHAR** ppContainingAssemblyName);

extern "C" BOOL __stdcall TryResolveAppDomainInfoSymbols(std::uint64_t profilerAppDomainId,
                                                         std::uint32_t appDomainNameBuffSize,
                                                         std::uint32_t* pActualAppDomainNameLen,
                                                         WCHAR* pAppDomainNameBuff,
                                                         std::uint64_t* pAppDomainProcessId,
                                                         bool* pSuccess);

extern "C" BOOL __stdcall TryGetThreadInfo(const std::uint32_t profilerThreadInfoId,
                                           std::int64_t* pClrThreadId,
                                           std::uint32_t* pOsThreadId,
                                           void** pOsThreadHandle,
                                           WCHAR* pThreadNameBuff,
                                           const std::uint32_t threadNameBuffSize,
                                           std::uint32_t* pActualThreadNameLen,
                                           bool* pSuccess);

extern "C" BOOL __stdcall GetAssemblyAndSymbolsBytes(void** ppAssemblyArray, int* pAssemblySize, void** ppSymbolsArray, int* pSymbolsSize, WCHAR* moduleName);

extern "C" HRESULT _stdcall TraceContextTracking_GetInfoFieldPointersForCurrentThread(const bool** ppIsNativeProfilerEngineActiveFlag,
                                                                                      std::uint64_t** ppCurrentTraceId,
                                                                                      std::uint64_t** ppCurrentSpanId);

/// <summary>
/// Each class inside of this class describes the infra so that the managed side can register an intry point into the anaged code.
/// </summary>
class ManagedCallbackRegistry
{
public:
    class EnqueueStackSnapshotBufferSegmentForExport
    {
    public:
        typedef HRESULT(_stdcall* Delegate_t)(void* segmentNativeObjectPtr,
                                              void* segmentMemory,
                                              std::uint32_t segmentByteCount,
                                              std::uint32_t segmentSnapshotCount,
                                              std::uint64_t segmentUnixTimeUtcRangeStart,
                                              std::uint64_t segmentUnixTimeUtcRangeEnd);

        static Delegate_t Set(Delegate_t pCallback);
        static bool TryInvoke(void* segmentNativeObjectPtr,
                              void* segmentMemory,
                              std::uint32_t segmentByteCount,
                              std::uint32_t segmentSnapshotCount,
                              std::uint64_t segmentUnixTimeUtcRangeStart,
                              std::uint64_t segmentUnixTimeUtcRangeEnd,
                              HRESULT* result);

    private:
        static std::mutex _invocationLock;
        static Delegate_t _pCallback;
    };

    class TryShutdownCurrentManagedProfilerEngine
    {
    public:
        typedef bool(__stdcall* Delegate_t)(void);

        static Delegate_t Set(Delegate_t pCallback);
        static bool TryInvoke(bool* result);

    private:
        static std::mutex _invocationLock;
        static Delegate_t _pCallback;
    };

    class SetCurrentManagedThreadName
    {
    public:
        typedef HRESULT(__stdcall* Delegate_t)(void* pThreadNameCharArr);

        static Delegate_t Set(Delegate_t pCallback);
        static bool TryInvoke(const char* pThreadNameCharArr, HRESULT* result);

    private:
        static std::mutex _invocationLock;
        static Delegate_t _pCallback;
    };
};

extern "C" void* __stdcall ManagedCallbackRegistry_EnqueueStackSnapshotBufferSegmentForExport_Set(void* pCallback);

extern "C" void* __stdcall ManagedCallbackRegistry_TryShutdownCurrentManagedProfilerEngine_Set(void* pCallback);

extern "C" void* __stdcall ManagedCallbackRegistry_SetCurrentManagedThreadName_Set(void* pCallback);