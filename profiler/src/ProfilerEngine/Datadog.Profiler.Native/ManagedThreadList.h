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
#include "ServiceBase.h"

class ManagedThreadList
    :
    public IManagedThreadList,
    public ServiceBase
{
public:
    ManagedThreadList(ICorProfilerInfo4* pCorProfilerInfo);
    ~ManagedThreadList();

private:
    ManagedThreadList() = delete;

public:
    const char* GetName() override;
    bool RegisterThread(std::shared_ptr<ManagedThreadInfo>& pThreadInfo) override;
    bool UnregisterThread(ThreadID clrThreadId, std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) override;
    bool SetThreadOsInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle) override;
    bool SetThreadName(ThreadID clrThreadId, const shared::WSTRING& threadName) override;
    uint32_t Count() override;
    uint32_t GetHighCountAndReset() override;
    uint32_t GetLowCountAndReset() override;
    uint32_t CreateIterator() override;
    std::shared_ptr<ManagedThreadInfo> LoopNext(uint32_t iterator) override;
    HRESULT TryGetCurrentThreadInfo(std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) override;
    std::shared_ptr<ManagedThreadInfo> GetOrCreate(ThreadID clrThreadId) override;
    bool TryGetThreadInfo(uint32_t osThreadId, std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) override;
    void ForEach(std::function<void (ManagedThreadInfo*)> callback) override;

private:
    const char* _serviceName = "ManagedThreadList";
    static const std::uint32_t DefaultThreadListSize;

private:
    bool StartImpl() override;
    bool StopImpl() override;

    // We do most operations under this lock.
    // We expect very little contention on this lock:
    // Modifying operations are expected to be rare and, especially in a thread-pooled architecture.
    // Reading (i.e., LoopNext(..)) happens from the same sampler-thread.
    std::recursive_mutex _mutex;

    // Threads are stored in a vector where new threads are added at the end
    // Also, threads are "directly" accessible from their CLR ThreadID via an index
    std::vector<std::shared_ptr<ManagedThreadInfo>> _threads;
    std::unordered_map<ThreadID, std::shared_ptr<ManagedThreadInfo>> _lookupByClrThreadId;
    std::unordered_map<uint32_t, std::shared_ptr<ManagedThreadInfo>> _lookupByOsThreadId;

    // An iterator is just a position in the vector corresponding to the next thread to be returned by LoopNext
    // so keep track of them in a vector of positions initialized to 0
    std::vector<uint32_t> _iterators;

    ICorProfilerInfo4* _pCorProfilerInfo;

    // Keep track of the highest/lowest number of threads
    // Will be reset each time the value is read
    uint32_t _highCount;
    uint32_t _lowCount;

private:
    void UpdateIterators(uint32_t pos);
    std::shared_ptr<ManagedThreadInfo> FindByClrId(ThreadID clrThreadId);
};
