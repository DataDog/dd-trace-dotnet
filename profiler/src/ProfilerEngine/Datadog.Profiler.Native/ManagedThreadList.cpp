// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedThreadList.h"
#include "Log.h"
#include "OpSysTools.h"


const std::uint32_t ManagedThreadList::FillFactorPercent = 20;
const std::uint32_t ManagedThreadList::MinBufferSize = 50;
const std::uint32_t ManagedThreadList::MinCompactionUsedIndex = 10;

ManagedThreadList::ManagedThreadList(ICorProfilerInfo4* pCorProfilerInfo) :
    _threadsData{new DirectAccessCollection<ManagedThreadInfo*>(MinBufferSize)},
    _nextFreeIndex{0},
    _activeThreadCount{0},
    _nextElementIteratorIndex{0},
    _pCorProfilerInfo{pCorProfilerInfo}
{
    _lookupByClrThreadId.reserve(MinBufferSize);
    _lookupByProfilerThreadInfoId.reserve(MinBufferSize);
    _pCorProfilerInfo->AddRef();
}

ManagedThreadList::~ManagedThreadList()
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    DirectAccessCollection<ManagedThreadInfo*>* threadsData = _threadsData;
    if (threadsData != nullptr)
    {
        _threadsData = nullptr;

        ManagedThreadInfo** pDataItem = nullptr;
        for (std::uint32_t i = 0; i < _nextFreeIndex; i++)
        {
            threadsData->TryGet(i, &pDataItem);

            if (*pDataItem != nullptr)
            {
                (*pDataItem)->Release();
                *pDataItem = nullptr;
            }
        }

        delete threadsData;
    }

    ICorProfilerInfo4* pCorProfilerInfo = _pCorProfilerInfo;
    if (pCorProfilerInfo != nullptr)
    {
        pCorProfilerInfo->Release();
        _pCorProfilerInfo = nullptr;
    }
}

const char* ManagedThreadList::GetName()
{
    return _serviceName;
}

bool ManagedThreadList::Start()
{
    // nothing special to start
    return true;
}

bool ManagedThreadList::Stop()
{
    // nothing special to stop
    return true;
}

bool ManagedThreadList::GetOrCreateThread(ThreadID clrThreadId)
{
    return GetOrCreateThread(clrThreadId, nullptr);
}

bool ManagedThreadList::GetOrCreateThread(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo)
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    ManagedThreadInfo* pExistingOrNewInfo;
    if (!TryFindThreadByClrThreadId(clrThreadId, &pExistingOrNewInfo))
    {
        if (!AddNewThread(clrThreadId, &pExistingOrNewInfo))
        {
            return false;
        }
    }

    if (ppThreadInfo != nullptr)
    {
        *ppThreadInfo = pExistingOrNewInfo;
    }

    return true;
}

bool ManagedThreadList::AddNewThread(ThreadID clrThreadId, ManagedThreadInfo** ppCreatedThreadInfo)
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    ManagedThreadInfo* pNewThreadInfo = new ManagedThreadInfo(clrThreadId);
    pNewThreadInfo->AddRef();

    if (_threadsData->TrySet(_nextFreeIndex, pNewThreadInfo))
    {
        _lookupByClrThreadId[clrThreadId] = pNewThreadInfo;
        _lookupByProfilerThreadInfoId[pNewThreadInfo->GetProfilerThreadInfoId()] = pNewThreadInfo;

        _nextFreeIndex++;
        _activeThreadCount++;

        if (ppCreatedThreadInfo != nullptr)
        {
            *ppCreatedThreadInfo = pNewThreadInfo;
        }

        return true;
    }

    ResizeAndCompactData();

    if (_threadsData->TrySet(_nextFreeIndex, pNewThreadInfo))
    {
        _lookupByClrThreadId[clrThreadId] = pNewThreadInfo;
        _lookupByProfilerThreadInfoId[pNewThreadInfo->GetProfilerThreadInfoId()] = pNewThreadInfo;

        _nextFreeIndex++;
        _activeThreadCount++;

        if (ppCreatedThreadInfo != nullptr)
        {
            *ppCreatedThreadInfo = pNewThreadInfo;
        }

        return true;
    }

    Log::Error("Cannot add new thread even after calling ResizeAndCompactData(): must be a bug!");
    pNewThreadInfo->Release();
    return false;
}

bool ManagedThreadList::UnregisterThread(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo)
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    std::uint32_t threadIndex;
    ManagedThreadInfo** pThreadInfoListEntry = nullptr;
    bool threadExists = TryFindThreadIndexInList(clrThreadId, 0, &threadIndex, &pThreadInfoListEntry);

    if (!threadExists)
    {
        Log::Error("ManagedThreadList: thread ", std::dec, clrThreadId, "cannot be unregister because not in the list");
        return false;
    }

    if (ppThreadInfo != nullptr)
    {
        *ppThreadInfo = *pThreadInfoListEntry;
        (*ppThreadInfo)->AddRef(); // Caller must release
    }

    // Remove from the ByClrThreadId lookup:
    _lookupByClrThreadId.erase((*pThreadInfoListEntry)->GetClrThreadId());

    // Remove from the ByProfilerThreadInfoId lookup:
    _lookupByProfilerThreadInfoId.erase((*pThreadInfoListEntry)->GetProfilerThreadInfoId());

    // Remove the item from the collection and then delete the object:
    (*pThreadInfoListEntry)->Release();
    *pThreadInfoListEntry = nullptr;

    // Decrement counter of items:
    _activeThreadCount--;

    // Optimization: If we removed the last item, we can update next insertion index accordingly.
    // (Otherwise we defer to a later compaction)
    if (threadIndex == _nextFreeIndex - 1)
    {
        _nextFreeIndex--;
    }

    // Check fragmentation and perform compaction if required:
    std::uint32_t fragAmnt = _nextFreeIndex - _activeThreadCount;
    std::uint32_t fragPercent = static_cast<std::uint32_t>((fragAmnt * 100.0) / _activeThreadCount);
    if (fragPercent > FillFactorPercent && _nextFreeIndex >= MinCompactionUsedIndex)
    {
        ResizeAndCompactData();
    }

    return true;
}

bool ManagedThreadList::SetThreadOsInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle)
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    //Log::Debug("ManagedThreadList::SetThreadInfo(" + std::to_string(clrThreadId)
    //    + ", " + std::to_string(pThreadInfo->GetClrThreadId())
    //    + "): start {_threadsData->Count()=" + std::to_string(_threadsData->Count())
    //    + ", _nextFreeIndex=" + std::to_string(_nextFreeIndex)
    //    + ", _activeThreadCount=" + std::to_string(_activeThreadCount)
    //    + ", _nextElementIteratorIndex=" + std::to_string(_nextElementIteratorIndex) + "}");

    ManagedThreadInfo* pExistingInfo = nullptr;
    if (!GetOrCreateThread(clrThreadId, &pExistingInfo))
    {
        Log::Error("ManagedThreadList: thread 0x", std::hex, clrThreadId, " cannot be associated to OS ID(0x", std::hex, osThreadId, std::dec, ") because not in the list");
        return false;
    }

    pExistingInfo->SetOsInfo(osThreadId, osThreadHandle);

    Log::Debug("ManagedThreadList::SetThreadOsInfo(clrThreadId: 0x", std::hex, clrThreadId,
               ", osThreadId: ", std::dec, osThreadId,
               ", osThreadHandle: 0x", std::hex, osThreadHandle, ")",
               " completed for ProfilerThreadInfoId=", std::dec, pExistingInfo->GetProfilerThreadInfoId(), ".");

    return true;
}

bool ManagedThreadList::SetThreadName(ThreadID clrThreadId, shared::WSTRING* pThreadName)
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    ManagedThreadInfo* pExistingInfo;
    if (!GetOrCreateThread(clrThreadId, &pExistingInfo))
    {
        Log::Error("ManagedThreadList: impossible to set thread 0x", std::hex, clrThreadId, " name to  \"", (pThreadName == nullptr ? WStr("null") : *pThreadName), "\") because not in the list");
        return false;
    }

    pExistingInfo->SetThreadName(pThreadName);

    Log::Debug("ManagedThreadList::SetThreadName(clrThreadId: 0x", std::hex, clrThreadId,
               ", pThreadName: \"", (pThreadName == nullptr ? WStr("null") : *pThreadName), "\")",
               " completed for ProfilerThreadInfoId=", pExistingInfo->GetProfilerThreadInfoId(), ".");

    return true;
}

std::uint32_t ManagedThreadList::Count(void) const
{
    return _activeThreadCount;
}

ManagedThreadInfo* ManagedThreadList::LoopNext(void)
{
    std::lock_guard<std::recursive_mutex> lock(_mutex);

    if (_activeThreadCount == 0)
    {
        return nullptr;
    }

    ManagedThreadInfo** pDataItem = nullptr;
    bool canGet = _nextElementIteratorIndex < _nextFreeIndex && _threadsData->TryGet(_nextElementIteratorIndex++, &pDataItem);
    while (canGet && *pDataItem == nullptr)
    {
        canGet = _nextElementIteratorIndex < _nextFreeIndex && _threadsData->TryGet(_nextElementIteratorIndex++, &pDataItem);
    }

    if (!canGet)
    {
        _nextElementIteratorIndex = 0;
        canGet = _nextElementIteratorIndex < _nextFreeIndex && _threadsData->TryGet(_nextElementIteratorIndex++, &pDataItem);
        while (canGet && *pDataItem == nullptr)
        {
            canGet = _nextElementIteratorIndex < _nextFreeIndex && _threadsData->TryGet(_nextElementIteratorIndex++, &pDataItem);
        }

        if (!canGet)
        {
            return nullptr;
        }
    }

    (*pDataItem)->AddRef(); // Caller must release

    return *pDataItem;
}

void ManagedThreadList::ResizeAndCompactData(void)
{
    // This helper function must be called under the update lock (_mutex)!

    // Compute size for new buffer:
    std::uint32_t newDataSize = (std::max)(static_cast<std::uint32_t>(_activeThreadCount * 0.01 * (100.0 + FillFactorPercent)), MinBufferSize);

    // Allocate new buffer and copy data from old to new buffer:
    DirectAccessCollection<ManagedThreadInfo*>* newThreadsData = new DirectAccessCollection<ManagedThreadInfo*>(newDataSize);
    std::uint32_t newNextFreeIndex = 0;
    std::uint32_t newNextElementIteratorIndex = _nextElementIteratorIndex;

    ManagedThreadInfo** ppThreadInfo = nullptr;
    for (std::uint32_t i = 0; i < _nextFreeIndex; i++)
    {
        _threadsData->TryGet(i, &ppThreadInfo);

        // As we copy, perform compaction:
        if (*ppThreadInfo == nullptr)
        {
            if (i < _nextElementIteratorIndex)
            {
                newNextElementIteratorIndex--;
            }
        }
        else
        {
            newThreadsData->TrySet(newNextFreeIndex, *ppThreadInfo);
            newNextFreeIndex++;
        }
    }

    // Swap old and new data:
    DirectAccessCollection<ManagedThreadInfo*>* oldThreadsData = _threadsData;

    _threadsData = newThreadsData;
    _nextFreeIndex = newNextFreeIndex;
    _activeThreadCount = newNextFreeIndex;
    _nextElementIteratorIndex = newNextElementIteratorIndex;

    delete oldThreadsData;
}

/// <summary>
/// See the comment in the header file at the declaration of the _lookupByProfilerThreadInfoId table.
/// </summary>
bool ManagedThreadList::TryFindThreadByProfilerThreadInfoId(std::uint32_t profilerThreadInfoId, ManagedThreadInfo** ppThreadInfo)
{
    if (_activeThreadCount < 1)
    {
        return false;
    }

    {
        std::lock_guard<std::recursive_mutex> lock(_mutex);

        std::unordered_map<std::uint32_t, ManagedThreadInfo*>::const_iterator elem = _lookupByProfilerThreadInfoId.find(profilerThreadInfoId);

        if (elem == _lookupByProfilerThreadInfoId.end())
        {
            return false;
        }
        else
        {
            if (ppThreadInfo != nullptr)
            {
                *ppThreadInfo = elem->second;
                (*ppThreadInfo)->AddRef(); // caller must release
            }
            return true;
        }
    }
}

bool ManagedThreadList::TryGetThreadInfo(const std::uint32_t profilerThreadInfoId,
                                         ThreadID* pClrThreadId,
                                         DWORD* pOsThreadId,
                                         HANDLE* pOsThreadHandle,
                                         WCHAR* pThreadNameBuff,
                                         const std::uint32_t threadNameBuffSize,
                                         std::uint32_t* pActualThreadNameLen)
{
    ManagedThreadInfo* pThreadInfo = nullptr;
    bool canFind = this->TryFindThreadByProfilerThreadInfoId(profilerThreadInfoId, &pThreadInfo);

    if (!canFind || pThreadInfo == nullptr)
    {
        return false;
    }

    if (pClrThreadId != nullptr)
    {
        *pClrThreadId = pThreadInfo->GetClrThreadId();
    }

    if (pOsThreadId != nullptr)
    {
        *pOsThreadId = pThreadInfo->GetOsThreadId();
    }

    if (pOsThreadHandle != nullptr)
    {
        *pOsThreadHandle = pThreadInfo->GetOsThreadHandle();
    }

    const shared::WSTRING& tName = pThreadInfo->GetThreadName();

    if (pThreadNameBuff != nullptr && threadNameBuffSize > 0)
    {
        std::uint32_t copyCharCount = (std::min)(static_cast<std::uint32_t>(tName.size()), threadNameBuffSize - 1);

        // If a managed thread name was set, we will use it.
        // If a managed thread name was not set, we will attempt to use a potentially set native thread name (or debugger thread description).
        // Note that we do not get callbacks for the update of the latter, so we have to query it every time.
        // However, the results of this TryGetThreadInfo(..) method are cached on the managed side.
        // As a result, this API should not be called more than once per export cycle per thread, and we do not expect much overhead from this query.
        // (But native thread name updates may propagate with a delay).
        if (copyCharCount > 0)
        {
            tName.copy(pThreadNameBuff, copyCharCount, 0);
            pThreadNameBuff[copyCharCount] = static_cast<WCHAR>(0);
        }
        else
        {
            if (false == OpSysTools::GetNativeThreadName(pThreadInfo->GetOsThreadHandle(), pThreadNameBuff, threadNameBuffSize))
            {
                pThreadNameBuff[0] = static_cast<WCHAR>(0);
            }
        }
    }

    if (pActualThreadNameLen != nullptr)
    {
        *pActualThreadNameLen = static_cast<std::uint32_t>(tName.size());
    }

    pThreadInfo->Release();
    return true;
}

HRESULT ManagedThreadList::TryGetCurrentThreadInfo(ManagedThreadInfo** ppThreadInfo)
{
    ThreadID clrThreadId;
    HRESULT hr = _pCorProfilerInfo->GetCurrentThreadID(&clrThreadId);
    if (FAILED(hr))
    {
        return hr;
    }

    if (clrThreadId == 0)
    {
        return E_FAIL;
    }

    if (TryFindThreadByClrThreadId(clrThreadId, ppThreadInfo))
    {
        return S_OK;
    }
    else
    {
        return S_FALSE;
    }
}

bool ManagedThreadList::TryFindThreadByClrThreadId(ThreadID clrThreadId, ManagedThreadInfo** ppThreadInfo)
{
    // This helper method is called from modifying fucntions under the update lock (_mutex)!

    // If there is nothing in the list, fail fast:
    if (_nextFreeIndex < 1)
    {
        return false;
    }

    // Optimization. Try look at a few last list entries before falling back to the table lookup...
    static const std::uint32_t MaxScanOptimizationLength = 10;

    std::uint32_t minIndex = (_nextFreeIndex <= MaxScanOptimizationLength) ? 0 : _nextFreeIndex - MaxScanOptimizationLength - 1;
    ManagedThreadInfo** pThreadInfoListEntry;
    std::uint32_t threadIndexUnused;

    if (TryFindThreadIndexInList(clrThreadId, minIndex, &threadIndexUnused, &pThreadInfoListEntry))
    {
        if (ppThreadInfo != nullptr)
        {
            *ppThreadInfo = *pThreadInfoListEntry;
        }

        return true;
    }

    // The thread id we are looking for is not in the last MaxScanOptimizationLength elements of the collection.
    // Use the lookup table:
    std::unordered_map<ThreadID, ManagedThreadInfo*>::const_iterator elem = _lookupByClrThreadId.find(clrThreadId);

    if (elem == _lookupByClrThreadId.end())
    {
        return false;
    }
    else
    {
        if (ppThreadInfo != nullptr)
        {
            *ppThreadInfo = elem->second;
        }

        return true;
    }
}

bool ManagedThreadList::TryFindThreadIndexInList(ThreadID clrThreadId,
                                                 std::uint32_t minIndex,
                                                 std::uint32_t* pThreadIndex,
                                                 ManagedThreadInfo*** pppThreadInfo)
{
    // This helper method is called from modifying fucntions under the update lock (_mutex)!

    // If there is nothing in the list, fail fast:
    if (_nextFreeIndex < 1)
    {
        return false;
    }

    ManagedThreadInfo** pThreadInfoListEntry = nullptr;
    std::uint32_t ind = _nextFreeIndex;
    while (ind > minIndex)
    {
        ind--;
        _threadsData->TryGet(ind, &pThreadInfoListEntry);

        // If we found the thread we looked for:
        if ((*pThreadInfoListEntry) != nullptr && (*pThreadInfoListEntry)->GetClrThreadId() == clrThreadId)
        {
            *pThreadIndex = ind;
            *pppThreadInfo = pThreadInfoListEntry;
            return true;
        }
    }

    return false;
}
