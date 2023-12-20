// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrEventsParser.h"

#include <iomanip>
#include <iostream>
#include <sstream>

#include "IAllocationsListener.h"
#include "IContentionListener.h"
#include "Log.h"
#include "OpSysTools.h"


const bool LogGcEvents = true;
#define LOG_GC_EVENT(x)                         \
{                                               \
    if (LogGcEvents)                            \
    {                                           \
        std::stringstream builder;              \
        builder << OpSysTools::GetThreadId()    \
        << " " << ((_gcInProgress.Number != -1) ? "F" : ((_currentBGC.Number != -1) ? "B" : ""))   \
        << GetCurrentGC().Number                \
        << " | " << x << std::endl;             \
        std::cout << builder.str();             \
    }                                           \
}                                               \


ClrEventsParser::ClrEventsParser(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener)
    :
    _pAllocationListener{pAllocationListener},
    _pContentionListener{pContentionListener},
    _pGCSuspensionsListener{pGCSuspensionsListener}
{
    ClearCurrentGC();
}

void ClrEventsParser::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    if (pGarbageCollectionsListener == nullptr)
    {
        return;
    }

    _pGarbageCollectionsListeners.push_back(pGarbageCollectionsListener);
}


void ClrEventsParser::ParseEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData
    )
{
    if (KEYWORD_GC == (keywords & KEYWORD_GC))
    {
        ParseGcEvent(timestamp, id, version, cbEventData, eventData);
    }
    else if (KEYWORD_CONTENTION == (keywords & KEYWORD_CONTENTION))
    {
        ParseContentionEvent(id, version, cbEventData, eventData);
    }
}

uint64_t ClrEventsParser::GetCurrentTimestamp()
{
    return OpSysTools::GetHighPrecisionTimestamp();
}

// TL;DR Deactivate the alignment check in the Undefined Behavior Sanitizers for the ParseGcEvent function
// because events fields are not aligned in the bitstream sent by the CLR.
//
// The UBSAN jobs crashes with this error message:
//
// runtime error: reference binding to misaligned address 0x7ffc64aa6442 for type 'uint64_t' (aka 'unsigned long'), which requires 8 byte alignment
// 0x7ffc64aa6442: note: pointer points here
//  00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00  00 00 00 00 00 00
//               ^
//     #0 0x7f17dc88a39a in ClrEventsParser::ParseGcEvent()
//     #1 0x7f17dc889f78 in ClrEventsParser::ParseEvent()
//     #2 0x7f17dc8bd9e4 in CorProfilerCallback::EventPipeEventDelivered()
//
#if defined(__clang__) || defined(DD_SANITIZERS)
__attribute__((no_sanitize("alignment")))
#endif
void
ClrEventsParser::ParseGcEvent(uint64_t timestamp, DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
    // look for AllocationTick_V4
    if ((id == EVENT_ALLOCATION_TICK) && (version == 4))
    {
        if (_pAllocationListener == nullptr)
        {
            return;
        }

        //template tid = "GCAllocationTick_V4" >
        //    <data name = "AllocationAmount" inType = "win:UInt32" />
        //    <data name = "AllocationKind" inType = "win:UInt32" />
        //    <data name = "ClrInstanceID" inType = "win:UInt16" />
        //    <data name = "AllocationAmount64" inType = "win:UInt64"/>
        //    <data name = "TypeID" inType = "win:Pointer" />
        //    <data name = "TypeName" inType = "win:UnicodeString" />
        //    <data name = "HeapIndex" inType = "win:UInt32" />
        //    <data name = "Address" inType = "win:Pointer" />
        //    <data name = "ObjectSize" inType = "win:UInt64" />
        //DumpBuffer(pEventData, cbEventData);

        AllocationTickV4Payload payload{0};
        ULONG offset = 0;
        if (!Read(payload.AllocationAmount, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.AllocationKind, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.ClrInstanceId, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.AllocationAmount64, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.TypeId, pEventData, cbEventData, offset))
        {
            return;
        }
        payload.TypeName = ReadWideString(pEventData, cbEventData, &offset);
        if (payload.TypeName == nullptr)
        {
            return;
        }
        if (!Read(payload.HeapIndex, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.Address, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.ObjectSize, pEventData, cbEventData, offset))
        {
            return;
        }
        _pAllocationListener->OnAllocation(
            payload.AllocationKind,
            payload.TypeId,
            payload.TypeName,
            payload.Address,
            payload.ObjectSize,
            payload.AllocationAmount64);

        return;
    }

    // the rest of events are related to garbage collections lifetime
    // read https://medium.com/criteo-engineering/spying-on-net-garbage-collector-with-net-core-eventpipes-9f2a986d5705?source=friends_link&sk=baf9a7766fb5c7899b781f016803597f
    // for more details about the state machine
    //
    if (id == EVENT_GC_TRIGGERED)
    {
        LOG_GC_EVENT("OnGCTriggered");
        OnGCTriggered();
    }
    else if (id == EVENT_GC_START)
    {
        GCStartPayload payload{0};
        ULONG offset = 0;
        if (!Read<GCStartPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        std::stringstream buffer;
        buffer << "OnGCStart: " << payload.Count << " " << payload.Depth << " " << payload.Reason << " " << payload.Type;
        LOG_GC_EVENT(buffer.str());
        OnGCStart(timestamp, payload);
    }
    else if (id == EVENT_GC_END)
    {
        GCEndPayload payload{0};
        ULONG offset = 0;
        if (!Read<GCEndPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        std::stringstream buffer;
        buffer << "OnGCEnd: " << payload.Count << " " << payload.Depth;
        LOG_GC_EVENT(buffer.str());
        OnGCEnd(payload);
    }
    else if (id == EVENT_GC_SUSPEND_EE_BEGIN)
    {
        LOG_GC_EVENT("OnGCSuspendEEBegin");
        OnGCSuspendEEBegin(timestamp);
    }
    else if (id == EVENT_GC_RESTART_EE_END)
    {
        LOG_GC_EVENT("OnGCRestartEEEnd");
        OnGCRestartEEEnd(timestamp);
    }
    else if (id == EVENT_GC_HEAP_STAT)
    {
        // This event provides the size of each generation after the collection
        // --> not used today but could be interesting to detect leaks (i.e. gen2/LOH/POH are growing)

        // TODO: check for size and see if V2 with POH numbers could be read from payload
        GCHeapStatsV1Payload payload = {0};
        ULONG offset = 0;
        if (!Read<GCHeapStatsV1Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        LOG_GC_EVENT("OnGCHeapStats");
        OnGCHeapStats(timestamp);
    }
    else if (id == EVENT_GC_GLOBAL_HEAP_HISTORY)
    {
        GCGlobalHeapPayload payload = {0};
        ULONG offset = 0;
        if (!Read<GCGlobalHeapPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        LOG_GC_EVENT("OnGCGlobalHeapHistory");
        OnGCGlobalHeapHistory(timestamp, payload);
    }
}

void ClrEventsParser::ParseContentionEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
    if (_pContentionListener == nullptr)
    {
        return;
    }

    // look for ContentionStop_V1
    if ((id == EVENT_CONTENTION_STOP) && (version >= 1))
    {
        //<template tid="ContentionStop_V1">
        //    <data name="ContentionFlags" inType="win:UInt8" />
        //    <data name="ClrInstanceID" inType="win:UInt16" />
        //    <data name="DurationNs" inType="win:Double" />
        //DumpBuffer(pEventData, cbEventData);

        ContentionStopV1Payload payload{0};
        ULONG offset = 0;
        if (!Read<ContentionStopV1Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        _pContentionListener->OnContention(payload.DurationNs);
    }
}

void ClrEventsParser::NotifySuspension(uint64_t timestamp, uint32_t number, uint32_t generation, uint64_t duration)
{
    if (_pGCSuspensionsListener != nullptr)
    {
        _pGCSuspensionsListener->OnSuspension(timestamp, number, generation, duration);
    }
}

void ClrEventsParser::NotifyGarbageCollectionStarted(uint64_t timestamp, int32_t number, uint32_t generation, GCReason reason, GCType type)
{
    for (auto& pGarbageCollectionsListener : _pGarbageCollectionsListeners)
    {
        pGarbageCollectionsListener->OnGarbageCollectionStart(timestamp, number, generation, reason, type);
    }
}

void ClrEventsParser::NotifyGarbageCollectionEnd(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    uint64_t pauseDuration,
    uint64_t totalDuration,
    uint64_t endTimestamp
    )
{
    for (auto& pGarbageCollectionsListener : _pGarbageCollectionsListeners)
    {
        pGarbageCollectionsListener->OnGarbageCollectionEnd(
            number,
            generation,
            reason,
            type,
            isCompacting,
            pauseDuration,
            totalDuration,
            endTimestamp
            );
    }
}

GCDetails& ClrEventsParser::GetCurrentGC()
{
    if (_gcInProgress.Number != -1)
    {
        return _gcInProgress;
    }

    return _currentBGC;
}

void ClrEventsParser::ClearCurrentGC()
{
    if (_gcInProgress.Number != -1)
    {
        ResetGC(_gcInProgress);
    }
    else
    {
        ResetGC(_currentBGC);
    }
}

void ClrEventsParser::ResetGC(GCDetails& gc)
{
    gc.Number = -1;
    gc.Generation = 0;
    gc.Reason = (GCReason)0;
    gc.Type = (GCType)0;
    gc.IsCompacting = false;
    gc.PauseDuration = 0;
    gc.StartTimestamp = 0;
    gc.HasGlobalHeapHistoryBeenReceived = false;
    gc.HasHeapStatsBeenReceived = false;
}

void ClrEventsParser::InitializeGC(uint64_t timestamp, GCDetails& gc, GCStartPayload& payload)
{
    gc.Number = payload.Count;
    gc.Generation = payload.Depth;
    gc.Reason = (GCReason)payload.Reason;
    gc.Type = (GCType)payload.Type;
    gc.IsCompacting = false;
    gc.PauseDuration = 0;
    gc.StartTimestamp = timestamp;
    gc.HasGlobalHeapHistoryBeenReceived = false;
    gc.HasHeapStatsBeenReceived = false;
}

void ClrEventsParser::OnGCTriggered()
{
}

void ClrEventsParser::OnGCStart(uint64_t timestamp, GCStartPayload& payload)
{
    NotifyGarbageCollectionStarted(
        timestamp,
        payload.Count,
        payload.Depth,
        static_cast<GCReason>(payload.Reason),
        static_cast<GCType>(payload.Type)
        );

    if ((payload.Depth == 2) && (payload.Type == GCType::BackgroundGC))
    {
        InitializeGC(timestamp, _currentBGC, payload);
    }
    else
    {
        // If a BCG is already started, nonConcurrent and Foreground GCs (0/1) are possible and will finish before the BGC
        InitializeGC(timestamp, _gcInProgress, payload);
    }
}

void ClrEventsParser::OnGCEnd(GCEndPayload& payload)
{
}

void ClrEventsParser::OnGCSuspendEEBegin(uint64_t timestamp)
{
    // we don't know yet what will be the next GC corresponding to this suspension
    // so it is kept until next GCStart
    _suspensionStart = timestamp;
}

void ClrEventsParser::OnGCRestartEEEnd(uint64_t timestamp)
{
    GCDetails& gc = GetCurrentGC();
    if (gc.Number == -1)
    {
        // this might happen (seen in workstation + concurrent mode)
        // --> just skip the suspension because we can associate to a GC
        _suspensionStart = 0;
        return;
    }

    // compute suspension time
    uint64_t suspensionDuration = 0;
    if (_suspensionStart != 0)
    {
        suspensionDuration = timestamp - _suspensionStart;
        NotifySuspension(timestamp, gc.Number, gc.Generation, suspensionDuration);

        _suspensionStart = 0;
    }
    else
    {
        // bad luck: a xxxBegin event has been missed
        LOG_GC_EVENT("### missing Begin event");
    }
    gc.PauseDuration += suspensionDuration;
}

void ClrEventsParser::OnGCHeapStats(uint64_t timestamp)
{
    // Note: last event for non background GC (will be GcGlobalHeapHistory for background gen 2)
    GCDetails& gc = GetCurrentGC();
    gc.HasHeapStatsBeenReceived = true;
    if (gc.Number == -1)
    {
        return;
    }

    if (gc.HasGlobalHeapHistoryBeenReceived)
    {
            std::stringstream buffer;
            buffer << "   end of GC #" << gc.Number << " - " << (timestamp - gc.StartTimestamp) / 1000000 << "ms";
            LOG_GC_EVENT(buffer.str());

            NotifyGarbageCollectionEnd(
                gc.Number,
                gc.Generation,
                gc.Reason,
                gc.Type,
                gc.IsCompacting,
                gc.PauseDuration,
                timestamp - gc.StartTimestamp,
                timestamp
                );
            ResetGC(gc);
    }
}

void ClrEventsParser::OnGCGlobalHeapHistory(uint64_t timestamp, GCGlobalHeapPayload& payload)
{
    GCDetails& gc = GetCurrentGC();

    // check unexpected event (we should have received a GCStart first)
    if (gc.Number == -1)
    {
        return;
    }
    gc.HasGlobalHeapHistoryBeenReceived = true;

    // check if the collection was compacting
    gc.IsCompacting =
        (payload.GlobalMechanisms & GCGlobalMechanisms::Compaction) == GCGlobalMechanisms::Compaction;

    if (gc.HasHeapStatsBeenReceived)
    {
        std::stringstream buffer;
        buffer << "   end of GC #" << gc.Number << " - " << (timestamp - gc.StartTimestamp) / 1000000 << "ms";
        LOG_GC_EVENT(buffer.str());

        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            timestamp - gc.StartTimestamp,
            timestamp);
        ResetGC(gc);
    }
}
