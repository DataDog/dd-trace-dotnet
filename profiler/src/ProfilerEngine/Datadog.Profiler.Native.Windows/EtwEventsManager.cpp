// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.


#include "EtwEventsManager.h"
#include "IContentionListener.h"
#include "IAllocationsListener.h"
#include "IGCSuspensionsListener.h"

#include "Windows.h"

#include <string>

const std::string NamedPipePrefix = "\\\\.\\pipe\\DD_ETW_CLIENT_";
const std::string NamedPipeAgent = "\\\\.\\pipe\\DD_ETW_DISPATCHER";


EtwEventsManager::EtwEventsManager(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener)
    :
    _pAllocationListener(pAllocationListener),
    _pContentionListener(pContentionListener)
{
    _threadsInfo.reserve(256);
    _parser = std::make_unique<ClrEventsParser>(
        nullptr,  // to avoid duplicates with what is done in EtwEventsHandler
        nullptr,  // to avoid duplicates with what is done in EtwEventsHandler
        pGCSuspensionsListener);
}

void EtwEventsManager::OnEvent(
    uint64_t timestamp,
    uint32_t tid,
    uint32_t version,
    uint64_t keyword,
    uint8_t level,
    uint32_t id,
    uint32_t cbEventData,
    const uint8_t* pEventData)
{
    if (keyword == KEYWORD_STACKWALK)
    {
        auto pThreadInfo = GetOrCreate(tid);
        if (pThreadInfo->LastEventWasContentionStart())
        {
            AttachContentionCallstack(pThreadInfo, cbEventData, pEventData);
        }
        else if (pThreadInfo->LastEventWasAllocationTick())
        {
            AttachAllocationCallstack(pThreadInfo, cbEventData, pEventData);
        }

        pThreadInfo->ClearLastEventId();
    }
    else if (keyword == KEYWORD_CONTENTION)
    {
        if (_pContentionListener == nullptr)
        {
            return;
        }

        if (id == EVENT_CONTENTION_START)
        {
            auto pThreadInfo = GetOrCreate(tid);
            pThreadInfo->ContentionStartTimestamp = timestamp;
            pThreadInfo->LastEventId = EventId::ContentionStart;
        }
        else if (id == EVENT_CONTENTION_STOP)
        {
            auto pThreadInfo = Find(tid);
            if (pThreadInfo != nullptr)
            {
                pThreadInfo->ClearLastEventId();
                if (pThreadInfo->ContentionStartTimestamp != 0)
                {
                    auto duration = timestamp - pThreadInfo->ContentionStartTimestamp;
                    pThreadInfo->ContentionStartTimestamp = 0;

                    _pContentionListener->OnContention(timestamp, tid, duration, pThreadInfo->ContentionCallStack);
                }
            }
            else
            {
                // this should never happen
            }
        }
    }
    else if (keyword == KEYWORD_GC)
    {
        auto pThreadInfo = GetOrCreate(tid);

        if (id == EVENT_ALLOCATION_TICK)
        {
            if (_pAllocationListener == nullptr)
            {
                return;
            }

            pThreadInfo->LastEventId = EventId::AllocationTick;

            // TODO: get the type name and ClassID from the payload
            // TODO: update IAllocationListener to take the type name, ClassID and stack
        }
        else
        {
            // TODO: call only for no call stack GC events
            //_parser.get()->ParseEvent(version, keyword, id, cbEventData, pEventData);
        }
    }
}

ThreadInfo* EtwEventsManager::GetOrCreate(uint32_t tid)
{
    auto pInfo = Find(tid);
    if (pInfo == nullptr)
    {
        ThreadInfo threadInfo;
        threadInfo.ThreadId = tid;
        pInfo = &(_threadsInfo[tid] = threadInfo);
    }

    return pInfo;
}

ThreadInfo* EtwEventsManager::Find(uint32_t tid)
{
    auto infoEntry = _threadsInfo.find(tid);
    if (infoEntry != _threadsInfo.end())
    {
        return &(infoEntry->second);
    }

    return nullptr;
}

void EtwEventsManager::AttachCallstack(std::vector<uintptr_t>& stack, uint16_t userDataLength, const uint8_t* pUserData)
{
    stack.clear();

    if (userDataLength == 0)
    {
        return;
    }

    if (userDataLength < sizeof(StackWalkPayload))
    {
        return;
    }

    StackWalkPayload* pPayload = (StackWalkPayload*)pUserData;
    if (userDataLength < pPayload->FrameCount * sizeof(uintptr_t) + sizeof(StackWalkPayload))
    {
        return;
    }

    for (uint32_t i = 0; i < pPayload->FrameCount; ++i)
    {
        stack.push_back(pPayload->Stack[i]);
    }
}

void EtwEventsManager::AttachContentionCallstack(ThreadInfo* pThreadInfo, uint16_t userDataLength, const uint8_t* pUserData)
{
    AttachCallstack(pThreadInfo->ContentionCallStack, userDataLength, pUserData);
}

void EtwEventsManager::AttachAllocationCallstack(ThreadInfo* pThreadInfo, uint16_t userDataLength, const uint8_t* pUserData)
{
    AttachCallstack(pThreadInfo->AllocationCallStack, userDataLength, pUserData);
}


void EtwEventsManager::OnStop()
{
    // TODO: add some logs
}


void EtwEventsManager::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    _parser->Register(pGarbageCollectionsListener);
}


bool EtwEventsManager::Start()
{
    DWORD pid = ::GetCurrentProcessId();

    // TODO: start the server named pipe

    // TODO: contact the Agent named pipe

    return true;
}

void EtwEventsManager::Stop()
{
}