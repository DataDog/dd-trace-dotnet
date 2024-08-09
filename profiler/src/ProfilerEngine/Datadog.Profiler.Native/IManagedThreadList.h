// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "cor.h"
#include "corprof.h"

#include "ManagedThreadInfo.h"

#include <functional>
#include <memory>

class IManagedThreadList
{
public:
    virtual bool RegisterThread(std::shared_ptr<ManagedThreadInfo>& pThreadInfo) = 0;
    virtual bool UnregisterThread(ThreadID clrThreadId, std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) = 0;
    virtual bool SetThreadOsInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle) = 0;
    virtual bool SetThreadName(ThreadID clrThreadId, const shared::WSTRING& threadName) = 0;
    virtual uint32_t Count() = 0;
    virtual uint32_t GetHighCountAndReset() = 0;
    virtual uint32_t GetLowCountAndReset() = 0;
    virtual uint32_t CreateIterator() = 0;
    virtual std::shared_ptr<ManagedThreadInfo> LoopNext(uint32_t iterator) = 0;
    virtual HRESULT TryGetCurrentThreadInfo(std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) = 0;
    virtual std::shared_ptr<ManagedThreadInfo> GetOrCreate(ThreadID clrThreadId) = 0;
    virtual bool TryGetThreadInfo(uint32_t osThreadId, std::shared_ptr<ManagedThreadInfo>& ppThreadInfo) = 0;
    virtual void ForEach(std::function<void (ManagedThreadInfo*)> callback) = 0;
};