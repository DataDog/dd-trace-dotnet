// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <mutex>
#include <unordered_map>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "DirectAccessCollection.h"
#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"
#include "IManagedThreadList.h"


class ManagedThreadList : public IManagedThreadList
{
public:
    ManagedThreadList(ICorProfilerInfo4* pCorProfilerInfo);

private:
    ManagedThreadList() = delete;
    ~ManagedThreadList() override;

public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    bool GetOrCreateThread(ThreadID clrThreadId) override;
    bool UnregisterThread(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo) override;
    bool SetThreadOsInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle) override;
    bool SetThreadName(ThreadID clrThreadId, shared::WSTRING* pThreadName) override;
    std::uint32_t Count() const override;
    ManagedThreadInfo* LoopNext() override;
    bool TryGetThreadInfo(const std::uint32_t profilerThreadInfoId,
                          ThreadID* pClrThreadId,
                          DWORD* pOsThreadId,
                          HANDLE* pOsThreadHandle,
                          WCHAR* pThreadNameBuff,
                          const std::uint32_t threadNameBuffLen,
                          std::uint32_t* pActualThreadNameLen) override;
    HRESULT TryGetCurrentThreadInfo(ManagedThreadInfo** ppThreadInfo) override;

private:
    bool GetOrCreateThread(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo);

private:
    const char* _serviceName = "ManagedThreadList";
    static const std::uint32_t FillFactorPercent;
    static const std::uint32_t MinBufferSize;
    static const std::uint32_t MinCompactionUsedIndex;

private:
    // We do most operations under this lock.
    // We expect very little contention on this lock:
    // Modifying operations are expected to be rare and, expecially in a thread-pooled architecture.
    // Reading (i.e., LoopNext(..)) happens from the same sampler-thread.
    std::recursive_mutex _mutex;

    DirectAccessCollection<ManagedThreadInfo*>* _threadsData;
    std::uint32_t _nextFreeIndex;
    std::uint32_t _activeThreadCount;
    std::uint32_t _nextElementIteratorIndex;

    std::unordered_map<ThreadID, ManagedThreadInfo*> _lookupByClrThreadId;

    // ProfilerThreadInfoId is unique numeric ID of a ManagedThreadInfo record.
    // We cannot use the OS id, because we do not always have it, and we cannot use the Clr internal thread id,
    // because we do not want to architecturally restrict ourselves to never profile native threads in the future
    // + it could be reused for a different thread by the CLR since this is the value of the pointer to the internal
    // representation of the managed thread.
    //
    // We tag all collected stack samples using the ProfilerThreadInfoId.
    // When the managed engine subsequently processes the stack samples, it may request
    // the info from the corresponding ManagedThreadInfo.
    // When that happens, we use the '_lookupByProfilerThreadInfoId' table to look up the ManagedThreadInfo instance
    // that corresponds to the id. If the thread is dead, it will no longer be in the table.
    std::unordered_map<std::uint32_t, ManagedThreadInfo*> _lookupByProfilerThreadInfoId;

    ICorProfilerInfo4* _pCorProfilerInfo;

private:
    void ResizeAndCompactData(void);
    bool AddNewThread(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo);
    bool TryFindThreadByClrThreadId(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo);
    bool TryFindThreadIndexInList(ThreadID clrThreadId, std::uint32_t minIndex, std::uint32_t* pThreadIndex, ManagedThreadInfo*** pppThreadInfo);
    bool TryFindThreadByProfilerThreadInfoId(std::uint32_t profilerThreadInfoId, ManagedThreadInfo** ppThreadInfo);
};
