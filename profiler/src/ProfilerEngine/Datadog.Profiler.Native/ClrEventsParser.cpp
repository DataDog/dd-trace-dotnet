// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrEventsParser.h"

#include <chrono>
#include <iomanip>
#include <iostream>
#include <sstream>

#include "IAllocationsListener.h"
#include "IContentionListener.h"
#include "EventsParserHelper.h"
#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"

using namespace std::chrono_literals;

// set to true for debugging purpose
constexpr bool LogGcEvents = false;

template <typename... Args>
void ClrEventsParser::LogGcEvent(
    Args const&... args)
{
    if constexpr (!LogGcEvents)
    {
        return;
    }

    std::cout
        << OpSysTools::GetThreadId()
        << " " << ((_gcInProgress.Number != -1) ? "F" : ((_currentBGC.Number != -1) ? "B" : ""))
        << GetCurrentGC().Number
        << " | ";
    (std::cout << ... << args);
    std::cout << std::endl;
}

ClrEventsParser::ClrEventsParser(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener,
    IGCDumpListener* pGCDumpListener)
    :
    _pAllocationListener{pAllocationListener},
    _pContentionListener{pContentionListener},
    _pGCSuspensionsListener{pGCSuspensionsListener},
    _pGCDumpListener{pGCDumpListener}
{
    ResetGC(_gcInProgress);
    ResetGC(_currentBGC);
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
    std::chrono::nanoseconds timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData)
{
    if (
        (KEYWORD_GC == (keywords & KEYWORD_GC)) ||
        (KEYWORD_GCHEAPDUMP == (keywords & KEYWORD_GCHEAPDUMP))  // GCBulkXXX events are treated as GC events
        )
    {
        ParseGcEvent(timestamp, id, version, cbEventData, eventData);
    }
    else if (KEYWORD_CONTENTION == (keywords & KEYWORD_CONTENTION))
    {
        ParseContentionEvent(id, version, cbEventData, eventData);
    }
    else if (KEYWORD_WAITHANDLE == (keywords & KEYWORD_WAITHANDLE))
    {
        ParseWaitHandleEvent(timestamp, id, version, cbEventData, eventData);
    }
    else if (KEYWORD_ALLOCATION_SAMPLING == (keywords & KEYWORD_ALLOCATION_SAMPLING))
    {
        ParseAllocationSampledEvent(timestamp, id, version, cbEventData, eventData);
    }
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


bool ClrEventsParser::ParseAllocationEvent(ULONG cbEventData, LPCBYTE pEventData, AllocationTickV4Payload& payload)
{
    // template tid = "GCAllocationTick_V4" >
    //     <data name = "AllocationAmount" inType = "win:UInt32" />
    //     <data name = "AllocationKind" inType = "win:UInt32" />
    //     <data name = "ClrInstanceID" inType = "win:UInt16" />
    //     <data name = "AllocationAmount64" inType = "win:UInt64"/>
    //     <data name = "TypeID" inType = "win:Pointer" />
    //     <data name = "TypeName" inType = "win:UnicodeString" />
    //     <data name = "HeapIndex" inType = "win:UInt32" />
    //     <data name = "Address" inType = "win:Pointer" />
    //     <data name = "ObjectSize" inType = "win:UInt64" />
    //
    // additional field for AllocationSampled event payload
    //     <data name="SampledByteOffset" inType="win:UInt64" />

    // DumpBuffer(pEventData, cbEventData);

    ULONG offset = 0;
    if (!EventsParserHelper::Read(payload.AllocationAmount, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.AllocationKind, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.ClrInstanceId, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.AllocationAmount64, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.TypeId, pEventData, cbEventData, offset))
    {
        return false;
    }
    payload.TypeName = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (payload.TypeName == nullptr)
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.HeapIndex, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.Address, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.ObjectSize, pEventData, cbEventData, offset))
    {
        return false;
    }

    return true;
}

bool ClrEventsParser::ParseAllocationSampledEvent(ULONG cbEventData, LPCBYTE pEventData, AllocationSampledPayload& payload)
{
    // <template tid = "AllocationSampled">
    //    <data name = "AllocationKind" inType = "win:UInt32" / >
    //    <data name = "ClrInstanceID" inType = "win:UInt16" / >
    //    <data name = "TypeID" inType = "win:Pointer" / >
    //    <data name = "TypeName" inType = "win:UnicodeString" / >
    //    <data name = "Address" inType = "win:Pointer" / >
    //    <data name = "ObjectSize" inType = "win:UInt64" / >
    //    <data name = "SampledByteOffset" inType = "win:UInt64" / >
    //

    // DumpBuffer(pEventData, cbEventData);

    ULONG offset = 0;
    if (!EventsParserHelper::Read(payload.AllocationKind, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.ClrInstanceId, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.TypeId, pEventData, cbEventData, offset))
    {
        return false;
    }
    payload.TypeName = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (payload.TypeName == nullptr)
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.Address, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.ObjectSize, pEventData, cbEventData, offset))
    {
        return false;
    }
    if (!EventsParserHelper::Read(payload.SampledByteOffset, pEventData, cbEventData, offset))
    {
        return false;
    }

    return true;
}

void
ClrEventsParser::ParseGcEvent(std::chrono::nanoseconds timestamp, DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
    // look for AllocationTick_V4
    if ((id == EVENT_ALLOCATION_TICK) && (version == 4))
    {
        if (_pAllocationListener == nullptr)
        {
            return;
        }

        AllocationTickV4Payload payload{0};
        if (!ParseAllocationEvent(cbEventData, pEventData, payload))
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

    // GC dump related events
    if (id == EVENT_GC_BULK_NODE)
    {
        // get the list of objects in the GC heap dump
        LogGcEvent("OnGCBulkNode");

        if (_pGCDumpListener != nullptr)
        {
            GCBulkNodePayload payload{0};
            ULONG offset = 0;
            if (!EventsParserHelper::Read<GCBulkNodePayload>(payload, pEventData, cbEventData, offset))
            {
                return;
            }

            // sanity check
            _pGCDumpListener->OnBulkNodes(
                payload.Index,
                payload.Count,
                (GCBulkNodeValue*)(pEventData + offset));
        }
    }
    else if (id == EVENT_GC_BULK_EDGE)
    {
        // get the list of references between objects in the GC heap dump
        LogGcEvent("OnGCBulkEdge");

        if (_pGCDumpListener != nullptr)
        {
            GCBulkEdgePayload payload{0};
            ULONG offset = 0;
            if (!EventsParserHelper::Read<GCBulkEdgePayload>(payload, pEventData, cbEventData, offset))
            {
                _pGCDumpListener->OnBulkEdges(
                    payload.Index,
                    payload.Count,
                    (GCBulkEdgeValue*)(pEventData + offset));
            }
        }
    }

    // the rest of events are related to garbage collections lifetime
    // read https://medium.com/criteo-engineering/spying-on-net-garbage-collector-with-net-core-eventpipes-9f2a986d5705?source=friends_link&sk=baf9a7766fb5c7899b781f016803597f
    // for more details about the state machine
    //
    if (id == EVENT_GC_TRIGGERED)
    {
        LogGcEvent("OnGCTriggered");
        OnGCTriggered();
    }
    else if (id == EVENT_GC_START)
    {
        GCStartPayload payload{0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<GCStartPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        LogGcEvent("OnGCStart: ", payload.Count, " ", payload.Depth, " ", payload.Reason, " ", payload.Type);
        OnGCStart(timestamp, payload);
    }
    else if (id == EVENT_GC_END)
    {
        GCEndPayload payload{0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<GCEndPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        LogGcEvent("OnGCEnd: ", payload.Count, " ", payload.Depth);

        OnGCEnd(payload);
    }
    else if (id == EVENT_GC_SUSPEND_EE_BEGIN)
    {
        LogGcEvent("OnGCSuspendEEBegin");
        OnGCSuspendEEBegin(timestamp);
    }
    else if (id == EVENT_GC_RESTART_EE_END)
    {
        LogGcEvent("OnGCRestartEEEnd");
        OnGCRestartEEEnd(timestamp);
    }
    else if (id == EVENT_GC_HEAP_STAT)
    {
        // This event provides the size of each generation after the collection
        // --> not used today but could be interesting to detect leaks (i.e. gen2/LOH/POH are growing)
        uint64_t gen2Size = 0;
        uint64_t lohSize = 0;
        uint64_t pohSize = 0;

        // check for size and see if V2 with POH numbers could be read from payload
        if (version == 1)
        {
            GCHeapStatsV1Payload payload = {0};
            ULONG offset = 0;
            if (!EventsParserHelper::Read<GCHeapStatsV1Payload>(payload, pEventData, cbEventData, offset))
            {
                return;
            }

            gen2Size = payload.GenerationSize2;
            lohSize = payload.GenerationSize3;
            pohSize = 0;
        }
        else if (version == 2)
        {
            GCHeapStatsV2Payload payload = {0};
            ULONG offset = 0;
            if (!EventsParserHelper::Read<GCHeapStatsV2Payload>(payload, pEventData, cbEventData, offset))
            {
                return;
            }

            gen2Size = payload.GenerationSize2;
            lohSize = payload.GenerationSize3;
            pohSize = payload.GenerationSize4;
        }

        LogGcEvent("OnGCHeapStats");
        OnGCHeapStats(timestamp, gen2Size, lohSize, pohSize);
    }
    else if (id == EVENT_GC_GLOBAL_HEAP_HISTORY)
    {
        GCGlobalHeapPayload payload = {0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<GCGlobalHeapPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        LogGcEvent("OnGCGlobalHeapHistory");
        OnGCGlobalHeapHistory(timestamp, payload);
    }
}

void ClrEventsParser::ParseAllocationSampledEvent(
    std::chrono::nanoseconds timestamp,
    DWORD id,
    DWORD version,
    ULONG cbEventData,
    LPCBYTE pEventData)
{
    if (_pAllocationListener == nullptr)
    {
        return;
    }

    // look for AllocationSampled event in .NET 10+
    if (id == EVENT_ALLOCATION_SAMPLED)
    {
        AllocationSampledPayload payload{0};
        if (!ParseAllocationSampledEvent(cbEventData, pEventData, payload))
        {
            return;
        }

        _pAllocationListener->OnAllocationSampled(
            payload.AllocationKind,
            payload.TypeId,
            payload.TypeName,
            payload.Address,
            payload.ObjectSize,
            payload.SampledByteOffset);
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
        // DumpBuffer(pEventData, cbEventData);

        ContentionStopV1Payload payload{0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<ContentionStopV1Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        _pContentionListener->OnContention(std::chrono::nanoseconds(std::llround(payload.DurationNs)));
    }

    if ((id == EVENT_CONTENTION_START) && (version >= 2))
    {
        ContentionStartV2Payload payload{0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<ContentionStartV2Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        _pContentionListener->SetBlockingThread(payload.LockOwnerThreadID);
    }
}

void ClrEventsParser::ParseWaitHandleEvent(std::chrono::nanoseconds timestamp, DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
    if (_pContentionListener == nullptr)
    {
        return;
    }

    if (id == EVENT_WAITHANDLE_START)
    {
        // <template tid="WaitHandleWaitStart">
        //     <data name="WaitSource" inType="win:UInt8" map="WaitHandleWaitSourceMap" />
        //     <data name="AssociatedObjectID" inType="win:Pointer" />
        //     data name="ClrInstanceID" inType="win:UInt16" />
        // </template>

        WaitHandleWaitStartPayload payload{0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<WaitHandleWaitStartPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        _pContentionListener->OnWaitStart(timestamp, payload.AssociatedObjectID);
    }
    else
    if (id == EVENT_WAITHANDLE_STOP)
    {
        // <template tid="WaitHandleWaitStart">
        //     data name="ClrInstanceID" inType="win:UInt16" />
        // </template>

        WaitHandleWaitStopPayload payload{0};
        ULONG offset = 0;
        if (!EventsParserHelper::Read<WaitHandleWaitStopPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        _pContentionListener->OnWaitStop(timestamp);
    }
}

void ClrEventsParser::NotifySuspension(std::chrono::nanoseconds timestamp, uint32_t number, uint32_t generation, std::chrono::nanoseconds duration)
{
    if (_pGCSuspensionsListener != nullptr)
    {
        _pGCSuspensionsListener->OnSuspension(timestamp, number, generation, duration);
    }
}

void ClrEventsParser::NotifyGarbageCollectionStarted(std::chrono::nanoseconds timestamp, int32_t number, uint32_t generation, GCReason reason, GCType type)
{
    for (auto& pGarbageCollectionsListener : _pGarbageCollectionsListeners)
    {
        LogGcEvent("OnGarbageCollectionStart: ", number, " ", generation, " ", reason, " ", type);

        pGarbageCollectionsListener->OnGarbageCollectionStart(timestamp, number, generation, reason, type);
    }
}

void ClrEventsParser::NotifyGarbageCollectionEnd(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    std::chrono::nanoseconds pauseDuration,
    std::chrono::nanoseconds totalDuration,
    std::chrono::nanoseconds endTimestamp,
    uint64_t gen2Size,
    uint64_t lohSize,
    uint64_t pohSize,
    uint32_t memPressure)
{
    for (auto& pGarbageCollectionsListener : _pGarbageCollectionsListeners)
    {
        LogGcEvent("OnGarbageCollectionEnd: #", number, " gen", generation, " ", reason, " ", type, " ", memPressure, "%");

        pGarbageCollectionsListener->OnGarbageCollectionEnd(
            number,
            generation,
            reason,
            type,
            isCompacting,
            pauseDuration,
            totalDuration,
            endTimestamp,
            gen2Size,
            lohSize,
            pohSize,
            memPressure);
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

void ClrEventsParser::ResetGC(GCDetails& gc)
{
    gc.Number = -1;
    gc.Generation = 0;
    gc.Reason = static_cast<GCReason>(0);
    gc.Type = static_cast<GCType>(0);
    gc.IsCompacting = false;
    gc.PauseDuration = 0ns;
    gc.StartTimestamp = 0ns;
    gc.HasGlobalHeapHistoryBeenReceived = false;
    gc.HasHeapStatsBeenReceived = false;
    gc.gen2Size = 0;
    gc.lohSize = 0;
    gc.pohSize = 0;
    gc.memPressure = 0;
}

void ClrEventsParser::InitializeGC(std::chrono::nanoseconds timestamp, GCDetails& gc, GCStartPayload& payload)
{
    gc.Number = payload.Count;
    gc.Generation = payload.Depth;
    gc.Reason = (GCReason)payload.Reason;
    gc.Type = (GCType)payload.Type;
    gc.IsCompacting = false;
    gc.PauseDuration = 0ns;
    gc.StartTimestamp = timestamp;
    gc.HasGlobalHeapHistoryBeenReceived = false;
    gc.HasHeapStatsBeenReceived = false;
    gc.gen2Size = 0;
    gc.lohSize = 0;
    gc.pohSize = 0;
    gc.memPressure = 0;
}

void ClrEventsParser::OnGCTriggered()
{
}

void ClrEventsParser::OnGCStart(std::chrono::nanoseconds timestamp, GCStartPayload& payload)
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

void ClrEventsParser::OnGCSuspendEEBegin(std::chrono::nanoseconds timestamp)
{
    // we don't know yet what will be the next GC corresponding to this suspension
    // so it is kept until next GCStart
    _suspensionStart = timestamp;
}

void ClrEventsParser::OnGCRestartEEEnd(std::chrono::nanoseconds timestamp)
{
    GCDetails& gc = GetCurrentGC();
    if (gc.Number == -1)
    {
        // this might happen (seen in workstation + concurrent mode)
        // --> just skip the suspension because we can associate to a GC
        _suspensionStart = 0ns;
        return;
    }

    // compute suspension time
    auto suspensionDuration = 0ns;
    if (_suspensionStart != 0ns)
    {
        suspensionDuration = timestamp - _suspensionStart;
        NotifySuspension(timestamp, gc.Number, gc.Generation, suspensionDuration);

        _suspensionStart = 0ns;
    }
    else
    {
        // bad luck: a xxxBegin event has been missed
        LogGcEvent("### missing Begin event");
    }
    gc.PauseDuration += suspensionDuration;

    // could be the end of a gen0/gen1 or of a non concurrent gen2 GC
    if ((gc.Generation < 2) || (gc.Type == GCType::NonConcurrentGC))
    {
        auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(timestamp - gc.StartTimestamp).count();
        LogGcEvent("   end of GC #", gc.Number, "-", duration, "ms");

        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            timestamp - gc.StartTimestamp,
            timestamp,
            gc.gen2Size,
            gc.lohSize,
            gc.pohSize,
            gc.memPressure);
        ResetGC(gc);
    }
}

void ClrEventsParser::OnGCHeapStats(std::chrono::nanoseconds timestamp, uint64_t gen2Size, uint64_t lohSize, uint64_t pohSize)
{
    // Note: last event for non background GC (will be GcGlobalHeapHistory for background gen 2)
    GCDetails& gc = GetCurrentGC();
    gc.HasHeapStatsBeenReceived = true;
    if (gc.Number == -1)
    {
        return;
    }

    gc.gen2Size = gen2Size;
    gc.lohSize = lohSize;
    gc.pohSize = pohSize;
    if (gc.HasGlobalHeapHistoryBeenReceived && (gc.Generation == 2) && (gc.Type == GCType::BackgroundGC))
    {
        auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(timestamp - gc.StartTimestamp).count();
        LogGcEvent("   end of GC #", gc.Number, "-", duration, "ms");

        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            timestamp - gc.StartTimestamp,
            timestamp,
            gc.gen2Size,
            gc.lohSize,
            gc.pohSize,
            gc.memPressure);
        ResetGC(gc);
    }
}

void ClrEventsParser::OnGCGlobalHeapHistory(std::chrono::nanoseconds timestamp, GCGlobalHeapPayload& payload)
{
    GCDetails& gc = GetCurrentGC();

    // check unexpected event (we should have received a GCStart first)
    if (gc.Number == -1)
    {
        return;
    }
    gc.HasGlobalHeapHistoryBeenReceived = true;
    gc.memPressure = payload.MemPressure;

    // check if the collection was compacting
    gc.IsCompacting =
        (payload.GlobalMechanisms & GCGlobalMechanisms::Compaction) == GCGlobalMechanisms::Compaction;

    if (gc.HasHeapStatsBeenReceived && (gc.Generation == 2) && (gc.Type == GCType::BackgroundGC))
    {
        auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(timestamp - gc.StartTimestamp).count();
        LogGcEvent("   end of GC #", gc.Number, "-", duration, "ms");

        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            timestamp - gc.StartTimestamp,
            timestamp,
            gc.gen2Size,
            gc.lohSize,
            gc.pohSize,
            payload.MemPressure);
        ResetGC(gc);
    }
}
