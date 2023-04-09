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


const bool LogGcEvents = false;
#define LOG_GC_EVENT(x)                         \
{                                               \
    if (LogGcEvents)                            \
    {                                           \
        std::stringstream builder;              \
        builder << OpSysTools::GetThreadId()    \
        << " " << ((_gcInProgress.Number != -1) ? "F" : ((_currentBGC.Number != -1) ? "B" : " "))   \
        << GetCurrentGC().Number                \
        << " | " << x << std::endl;             \
        std::cout << builder.str();             \
    }                                           \
}                                               \


ClrEventsParser::ClrEventsParser(
    ICorProfilerInfo12* pCorProfilerInfo,
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener)
    :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pAllocationListener{pAllocationListener},
    _pContentionListener{pContentionListener},
    _pGCSuspensionsListener{pGCSuspensionsListener}
{
    ClearCollections();
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
    EVENTPIPE_PROVIDER provider,
    DWORD eventId,
    DWORD eventVersion,
    ULONG cbMetadataBlob,
    LPCBYTE metadataBlob,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread,
    ULONG numStackFrames,
    UINT_PTR stackFrames[])
{
    // Currently, only "Microsoft-Windows-DotNETRuntime" provider is used so no need to check.
    // However, during the test, a last (keyword=0 id=1 V1) event is sent from "Microsoft-DotNETCore-EventPipe".

    // These should be the same as eventId and eventVersion.
    // However it was not the case for the last event received from "Microsoft-DotNETCore-EventPipe".
    DWORD id;
    DWORD version;
    INT64 keywords; // used to filter out unneeded events.
    WCHAR* name;
    if (!TryGetEventInfo(metadataBlob, cbMetadataBlob, name, id, keywords, version))
    {
        return;
    }

    if (KEYWORD_GC == (keywords & KEYWORD_GC))
    {
        ParseGcEvent(id, version, cbEventData, eventData);
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
void ClrEventsParser::ParseGcEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
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

        LOG_GC_EVENT("OnGCStart");
        OnGCStart(payload);
    }
    else if (id == EVENT_GC_END)
    {
        GCEndPayload payload{0};
        ULONG offset = 0;
        if (!Read<GCEndPayload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

        LOG_GC_EVENT("OnGCStop");
        OnGCStop(payload);
    }
    else if (id == EVENT_GC_SUSPEND_EE_BEGIN)
    {
        LOG_GC_EVENT("OnGCSuspendEEBegin");
        OnGCSuspendEEBegin();
    }
    else if (id == EVENT_GC_RESTART_EE_END)
    {
        LOG_GC_EVENT("OnGCRestartEEEnd");
        OnGCRestartEEEnd();
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
        OnGCHeapStats();
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
        OnGCGlobalHeapHistory(payload);
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

bool ClrEventsParser::TryGetEventInfo(LPCBYTE pMetadata, ULONG cbMetadata, WCHAR*& name, DWORD& id, INT64& keywords, DWORD& version)
{
    if (pMetadata == nullptr || cbMetadata == 0)
    {
        return false;
    }

    ULONG offset = 0;
    if (!Read(id, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    // skip the name to read keyword and version
    name = ReadWideString(pMetadata, cbMetadata, &offset);

    if (!Read(keywords, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    if (!Read(version, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    return true;
}

void ClrEventsParser::NotifySuspension(uint32_t number, uint32_t generation, uint64_t duration, uint64_t timestamp)
{
    if (_pGCSuspensionsListener != nullptr)
    {
        _pGCSuspensionsListener->OnSuspension(number, generation, duration, timestamp);
    }
}

void ClrEventsParser::NotifyGarbageCollectionStarted(int32_t number, uint32_t generation, GCReason reason, GCType type)
{
    for (auto& pGarbageCollectionsListener : _pGarbageCollectionsListeners)
    {
        pGarbageCollectionsListener->OnGarbageCollectionStart(number, generation, reason, type);
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

void ClrEventsParser::ClearCollections()
{
    ResetGC(_currentBGC);
    ResetGC(_gcInProgress);
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
}

void ClrEventsParser::InitializeGC(GCDetails& gc, GCStartPayload& payload)
{
    gc.Number = payload.Count;
    gc.Generation = payload.Depth;
    gc.Reason = (GCReason)payload.Reason;
    gc.Type = (GCType)payload.Type;
    gc.IsCompacting = false;
    gc.PauseDuration = 0;
    gc.StartTimestamp = GetCurrentTimestamp();
}

void ClrEventsParser::OnGCTriggered()
{
    // all previous collections are finished
    ClearCollections();
}

void ClrEventsParser::OnGCStart(GCStartPayload& payload)
{
    NotifyGarbageCollectionStarted(
        payload.Count,
        payload.Depth,
        static_cast<GCReason>(payload.Reason),
        static_cast<GCType>(payload.Type)
        );

    // If a BCG is already started, FGC (0/1) are possible and will finish before the BGC
    //
    if ((payload.Depth == 2) && (payload.Type == GCType::BackgroundGC))
    {
        InitializeGC(_currentBGC, payload);
    }
    else
    {
        InitializeGC(_gcInProgress, payload);
    }
}

void ClrEventsParser::OnGCStop(GCEndPayload& payload)
{
}

void ClrEventsParser::OnGCSuspendEEBegin()
{
    // we don't know yet what will be the next GC corresponding to this suspension
    // so it is kept until next GCStart
    _suspensionStart = GetCurrentTimestamp();
}

void ClrEventsParser::OnGCRestartEEEnd()
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
    uint64_t currentTimestamp = GetCurrentTimestamp();
    if (_suspensionStart != 0)
    {
        suspensionDuration = currentTimestamp - _suspensionStart;
        NotifySuspension(gc.Number, gc.Generation, suspensionDuration, currentTimestamp);

        _suspensionStart = 0;
    }
    else
    {
        // bad luck: a xxxBegin event has been missed
        LOG_GC_EVENT("### missing Begin event");
    }
    gc.PauseDuration += suspensionDuration;

    // could be the end of a gen0/gen1 or of a non concurrent gen2 GC
    if ((gc.Generation < 2) || (gc.Type == GCType::NonConcurrentGC))
    {
        auto endTimestamp = GetCurrentTimestamp();
        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            endTimestamp - gc.StartTimestamp,
            endTimestamp
            );
        _gcInProgress.Number = -1;
        return;
    }
}

void ClrEventsParser::OnGCHeapStats()
{
    // Note: last event for non background GC (will be GcGlobalHeapHistory for background gen 2)
    GCDetails& gc = GetCurrentGC();
    if (gc.Number == -1)
    {
        return;
    }

    // this is the last event for a gen0/gen1 foreground collection during a background gen2 collections
    if ((_currentBGC.Number != -1) && (gc.Generation < 2))
    {
        auto endTimestamp = GetCurrentTimestamp();
        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            endTimestamp - gc.StartTimestamp,
            endTimestamp
            );
        ResetGC(_gcInProgress);
    }
}

void ClrEventsParser::OnGCGlobalHeapHistory(GCGlobalHeapPayload& payload)
{
    GCDetails& gc = GetCurrentGC();

    // check unexpected event (we should have received a GCStart first)
    if (gc.Number == -1)
    {
        return;
    }

    // check if the collection was compacting
    gc.IsCompacting =
        (payload.GlobalMechanisms & GCGlobalMechanisms::Compaction) == GCGlobalMechanisms::Compaction;

    // this is the last event for gen 2 background collections
    if ((payload.CondemnedGeneration == 2) && (gc.Type == GCType::BackgroundGC))
    {
        // check unexpected generation mismatch: should never occur
        if (gc.Generation != payload.CondemnedGeneration)
        {
            return;
        }

        auto endTimestamp = GetCurrentTimestamp();
        NotifyGarbageCollectionEnd(
            gc.Number,
            gc.Generation,
            gc.Reason,
            gc.Type,
            gc.IsCompacting,
            gc.PauseDuration,
            endTimestamp - gc.StartTimestamp,
            endTimestamp
            );

        ClearCollections();
    }
}
