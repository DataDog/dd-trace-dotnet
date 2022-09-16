// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrEventsParser.h"

#include <iomanip>
#include <iostream>
#include <sstream>

#include "OpSysTools.h"

#include "IAllocationsListener.h"
#include "IContentionListener.h"

#include "Log.h"

ClrEventsParser::ClrEventsParser(
    ICorProfilerInfo12* pCorProfilerInfo,
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGarbageCollectionsListener* pGarbageCollectionsListener,
    IGCSuspensionsListener* pGCSuspensionsListener)
    :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pAllocationListener{pAllocationListener},
    _pContentionListener{pContentionListener},
    _pGarbageCollectionsListener{pGarbageCollectionsListener},
    _pGCSuspensionsListener{pGCSuspensionsListener}
{
    ClearCollections();
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
    else
    {
        std::stringstream builder;
        builder << "Keyword = " << keywords << " - " << std::setw(3) << id << std::endl;
        std::cout << builder.str();
    }
}

uint64_t ClrEventsParser::GetCurrentTimestamp()
{
    return OpSysTools::GetHighPrecisionNanoseconds();
}

void ClrEventsParser::ParseGcEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
    if ((id == EVENT_ALLOCATION_TICK) && (version == 4))
    {
        if (_pAllocationListener == nullptr)
        {
            return;
        }

        // template tid = "GCAllocationTick_V4" >
        //    <data name = "AllocationAmount" inType = "win:UInt32" />
        //    <data name = "AllocationKind" inType = "win:UInt32" />
        //    <data name = "ClrInstanceID" inType = "win:UInt16" />
        //    <data name = "AllocationAmount64" inType = "win:UInt64"/>
        //    <data name = "TypeID" inType = "win:Pointer" />
        //    <data name = "TypeName" inType = "win:UnicodeString" />
        //    <data name = "HeapIndex" inType = "win:UInt32" />
        //    <data name = "Address" inType = "win:Pointer" />
        //    <data name = "ObjectSize" inType = "win:UInt64" />
        // DumpBuffer(pEventData, cbEventData);

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
        _pAllocationListener->OnAllocation(payload.AllocationKind, payload.TypeId, payload.TypeName, payload.Address, payload.ObjectSize);

        return;
    }

    // the rest of events are related to garbage collections lifetime
    if (id == EVENT_GC_TRIGGERED)
    {
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

        OnGCStart(payload);
    }
    else if (id == EVENT_GC_SUSPEND_EE_BEGIN)
    {
        OnGCSuspendEEBegin();
    }
    else if (id == EVENT_GC_RESTART_EE_END)
    {
        OnGCRestartEEEnd();
    }
    else if (id == EVENT_GC_HEAP_STAT)
    {
        // This event provides the size of each generation after the collection
        // --> not used today but could be interested to detect leaks (i.e. gen2/LOH/POH are growing)

        // TODO: check for size and see if V2 with POH numbers could be read from payload
        GCHeapStatsV1Payload payload = {0};
        ULONG offset = 0;
        if (!Read<GCHeapStatsV1Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }

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

        OnGCGlobalHeapHistory(payload);
    }
    else
    {
        //std::stringstream builder;
        //if (version != 0)
        //     builder << "? " << std::setw(3) << id << "_V" << version << std::endl;
        //else
        //    builder << "? " << std::setw(3) << id << std::endl;
        //std::cout << builder.str();
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
    if (pMetadata == NULL || cbMetadata == 0)
    {
        return false;
    }

    ULONG offset = 0;
    Read(id, pMetadata, cbMetadata, offset);

    // skip the name to read keyword and version
    name = ReadWideString(pMetadata, cbMetadata, &offset);
    Read(keywords, pMetadata, cbMetadata, offset);
    Read(version, pMetadata, cbMetadata, offset);

    return true;
}

void ClrEventsParser::NotifySuspension(uint32_t number, uint32_t generation, uint64_t duration, uint64_t timestamp)
{
    if (_pGCSuspensionsListener != nullptr)
    {
        _pGCSuspensionsListener->OnSuspension(number, generation, duration, timestamp);
    }
}

void ClrEventsParser::NotifyGarbageCollection(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    uint64_t pauseDuration,
    uint64_t timestamp)
{
    if (_pGarbageCollectionsListener != nullptr)
    {
        _pGarbageCollectionsListener->OnGarbageCollection(number, generation, reason, type, isCompacting, pauseDuration, timestamp);
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
    _suspensionStart = 0;
}

void ClrEventsParser::ResetGC(GCDetails& gc)
{
    gc.Number = -1;
    gc.Generation = 0;
    gc.Reason = (GCReason)0;
    gc.Type = (GCType)0;
    gc.IsCompacting = false;
    gc.PauseDuration;
    gc.Timestamp = 0;
}

void ClrEventsParser::InitializeGC(GCDetails& gc, GCStartPayload& payload)
{
    gc.Number = payload.Count;
    gc.Generation = payload.Depth;
    gc.Reason = (GCReason)payload.Reason;
    gc.Type = (GCType)payload.Type;
    gc.IsCompacting = false;
    gc.PauseDuration = 0;
    gc.Timestamp = GetCurrentTimestamp();
}

void ClrEventsParser::OnGCTriggered()
{
    std::cout << std::endl << "OnGCTriggered" << std::endl;

    // all previous collections are finished
    ClearCollections();
}

void ClrEventsParser::OnGCStart(GCStartPayload& payload)
{
    std::stringstream builder;

    builder << "OnGCStart(" << payload.Depth << ", " << payload.Type << ")"<< std::endl;
    std::cout << builder.str();

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
        // this should never happen, except if we are unlucky to have missed a GCStart event
        return;
    }

    // compute suspension time
    uint64_t suspensionDuration = 0;
    uint64_t currentTimestamp = currentTimestamp = GetCurrentTimestamp();
    if (_suspensionStart != 0)
    {
        suspensionDuration = currentTimestamp - _suspensionStart;
        NotifySuspension(gc.Number, gc.Generation, suspensionDuration, currentTimestamp);

        _suspensionStart = 0;
    }
    else
    {
        // bad luck: a xxxBegin event has been missed
    }
    gc.PauseDuration += suspensionDuration;

    // could be the end of a gen0/gen1 or of a non concurrent gen2 GC
    if (
        (gc.Generation < 2) ||
        (gc.Type == GCType::NonConcurrentGC))
    {
        NotifyGarbageCollection(gc.Number, gc.Generation, gc.Reason, gc.Type, gc.IsCompacting, gc.PauseDuration, gc.Timestamp);
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
    if (
        (_currentBGC.Number != -1) &&
        (gc.Generation < 2))
    {
        NotifyGarbageCollection(gc.Number, gc.Generation, gc.Reason, gc.Type, gc.IsCompacting, gc.PauseDuration, gc.Timestamp);
        ResetGC(_gcInProgress);
    }
}

void ClrEventsParser::OnGCGlobalHeapHistory(GCGlobalHeapPayload& payload)
{
    GCDetails& gc = GetCurrentGC();

    std::stringstream builder;
    builder << "OnGCGlobalHeapHistory(" << payload.CondemnedGeneration << ", " << gc.Type << ")" << std::endl;
    std::cout << builder.str();

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
            std::cout << "generation mismatch" << std::endl;
            return;
        }

        NotifyGarbageCollection(gc.Number, gc.Generation, gc.Reason, gc.Type, gc.IsCompacting, gc.PauseDuration, gc.Timestamp);

        ClearCollections();
    }
}
