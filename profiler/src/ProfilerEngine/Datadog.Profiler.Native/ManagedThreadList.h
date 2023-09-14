// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <mutex>
#include <unordered_map>
#include <vector>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"
#include "IManagedThreadList.h"


class ManagedThreadList : public IManagedThreadList
{
public:
    ManagedThreadList(ICorProfilerInfo4* pCorProfilerInfo);
    ~ManagedThreadList() override;

private:
    ManagedThreadList() = delete;

public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    bool GetOrCreateThread(ThreadID clrThreadId) override;
    bool RegisterThread(std::shared_ptr<ManagedThreadInfo>& pThreadInfo) override;
    bool UnregisterThread(ThreadID clrThreadId, std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) override;
    bool SetThreadOsInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle) override;
    bool SetThreadName(ThreadID clrThreadId, const shared::WSTRING& threadName) override;
    uint32_t Count() override;
    uint32_t CreateIterator() override;
    std::shared_ptr<ManagedThreadInfo> LoopNext(uint32_t iterator) override;
    HRESULT TryGetCurrentThreadInfo(std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) override;
    std::shared_ptr<ManagedThreadInfo> GetOrCreate(ThreadID clrThreadId);

private:
    const char* _serviceName = "ManagedThreadList";
    static const std::uint32_t MinBufferSize;

private:
    // We do most operations under this lock.
    // We expect very little contention on this lock:
    // Modifying operations are expected to be rare and, especially in a thread-pooled architecture.
    // Reading (i.e., LoopNext(..)) happens from the same sampler-thread.
    std::recursive_mutex _mutex;

    // Threads are stored in a vector where new threads are added at the end
    // Also, threads are "directly" accessible from their CLR ThreadID via an index
    std::vector<std::shared_ptr<ManagedThreadInfo>> _threads;
    std::unordered_map<ThreadID, std::shared_ptr<ManagedThreadInfo>> _lookupByClrThreadId;

    // An iterator is just a position in the vector corresponding to the next thread to be returned by LoopNext
    // so keep track of them in a vector of positions initialized to 0
    std::vector<uint32_t> _iterators;

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
    std::unordered_map<std::uint32_t, std::shared_ptr<ManagedThreadInfo>> _lookupByProfilerThreadInfoId;

    ICorProfilerInfo4* _pCorProfilerInfo;

private:
    void UpdateIterators(uint32_t pos);
    std::shared_ptr<ManagedThreadInfo> FindByClrId(ThreadID clrThreadId);
};
