// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "cor.h"
#include "corprof.h"

#include "IService.h"
#include "ManagedThreadInfo.h"


class IManagedThreadList : public IService
{
public:
    virtual bool GetOrCreateThread(ThreadID clrThreadId) = 0;
    virtual bool UnregisterThread(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo) = 0;
    virtual bool SetThreadOsInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle) = 0;
    virtual bool SetThreadName(ThreadID clrThreadId, shared::WSTRING* pThreadName) = 0;
    virtual std::uint32_t Count() const = 0;
    virtual ManagedThreadInfo* LoopNext() = 0;
    virtual bool TryGetThreadInfo(const std::uint32_t profilerThreadInfoId,
                          ThreadID* pClrThreadId,
                          DWORD* pOsThreadId,
                          HANDLE* pOsThreadHandle,
                          WCHAR* pThreadNameBuff,
                          const std::uint32_t threadNameBuffLen,
                          std::uint32_t* pActualThreadNameLen) = 0;
    virtual HRESULT TryGetCurrentThreadInfo(ManagedThreadInfo** ppThreadInfo) = 0;
};