// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IEtwEventsManager.h"
#include "ETW/IEtwEventsReceiver.h"
#include "ClrEventsParser.h"
#include "ETW/IpcClient.h"
#include "ETW/IpcServer.h"
#include "ETW/EtwEventsHandler.h"
#include "ETW/ProfilerLogger.h"


#include "Configuration.h"
#include <memory>


enum class EventId : uint32_t
{
    Unknown = 0,
    ContentionStart = 1,
    AllocationTick = 2
};


struct ThreadInfo
{
    // same key as in the map
    uint32_t ThreadId = 0;

    // bit field to identify the last received event for ClrStackWalk association
    EventId LastEventId = EventId::Unknown;

    // Lock contentions
    std::vector<uintptr_t> ContentionCallStack;
    uint64_t ContentionStartTimestamp = 0;

    // Allocations
    std::vector<uintptr_t> AllocationCallStack;
    uint64_t AllocationTickTimestamp = 0;
    uint32_t AllocationKind = 0;
    uintptr_t AllocationClassId = 0;
    std::string AllocatedType;
    uint64_t AllocationAmount = 0;

public:
    const inline bool LastEventWasContentionStart()
    {
        return LastEventId == EventId::ContentionStart;
    }
    const inline bool LastEventWasAllocationTick()
    {
        return LastEventId == EventId::AllocationTick;
    }
    inline void ClearLastEventId()
    {
        LastEventId = EventId::Unknown;
    }
};


class EtwEventsManager :
    public IEtwEventsManager,
    public IEtwEventsReceiver
{
public:
    EtwEventsManager(
        IAllocationsListener* pAllocationListener,
        IContentionListener* pContentionListener,
        IGCSuspensionsListener* pGCSuspensionsListener,
        IConfiguration* pConfiguration
        );

// Inherited via IEtwEventsManager
    void Register(IGarbageCollectionsListener* pGarbageCollectionsListener) override;
    bool Start() override;
    void Stop() override;

// Inherited via IEtwEventsReceiver
    void OnEvent(
        uint64_t systemTimestamp,
        uint32_t tid,
        uint32_t version,
        uint64_t keyword,
        uint8_t level,
        uint32_t id,
        uint32_t cbEventData,
        const uint8_t* pEventData) override;
    void OnStop() override;

private:
    ThreadInfo* GetOrCreate(uint32_t tid);
    ThreadInfo* Find(uint32_t tid);
    void AttachContentionCallstack(ThreadInfo* pThreadInfo, uint16_t userDataLength, const uint8_t* pUserData);
    void AttachAllocationCallstack(ThreadInfo* pThreadInfo, uint16_t userDataLength, const uint8_t* pUserData);
    void AttachCallstack(std::vector<uintptr_t>& stack, uint16_t userDataLength, const uint8_t* pUserData);
    bool SendRegistrationCommand(bool add);

 private:
    bool _isDebugLogEnabled;
    IAllocationsListener* _pAllocationListener;
    IContentionListener* _pContentionListener;

    // when no call stacks are needed and same payload format between Framework and Core (such as GC events)
    std::unique_ptr<ClrEventsParser> _parser;

    // injected logger
    std::unique_ptr<ProfilerLogger> _logger;

    // responsible for receiving ETW events from the Windows Agent
    std::unique_ptr<EtwEventsHandler> _eventsHandler;
    std::unique_ptr<IpcServer> _IpcServer; // used to connect to the Windows Agent and register our process ID
    std::unique_ptr<IpcClient> _IpcClient; // used to receive ETW events from the Windows Agent

private:
    // Each ClrStackWalk event is received at some point AFTER its sibling CLR event.
    // The key to match the two events is the thread id.
    // So, it is needed to keep a map of the last received CLR events per thread.
    // With that in place, when a ClrStackWalk event is received, it can be matched
    // with the last received CLR event on the same thread.
    //
    // For thread contention, the ClrStackWalk event is received AFTER the ContentionStart event
    // but not after the ContentionStop event. So, it is also needed to keep a map of the last
    // received ContentionStart events per thread. Finally, we have to to wait for the ContentionStop
    // event to get the contention duration by comparing the 2 timestamps.

    // For each thread id, keep track of the last ContentionStart event timestamp
    // and the last ContentionStart callstack. Also keep track of the AllocationTick payload.
    std::unordered_map<uint32_t, ThreadInfo> _threadsInfo;
    // No need to make it thread safe because the events are received sequentially by the same thread
};
