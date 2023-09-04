// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <map>
#include <math.h>
#include <shared_mutex>
#include <string>
#include <vector>

#include "GarbageCollection.h"
#include "IAllocationsListener.h"
#include "IGarbageCollectionsListener.h"
#include "IGCSuspensionsListener.h"

#include "EventPipe/DiagnosticsClient.h"
#include "OpSysTools.h"

#include "shared/src/native-src/string.h"
#include "assert.h"


#define LONG_LENGTH 1024

#pragma pack(1)
struct AllocationTickV4Payload
{
    uint32_t AllocationAmount;     // The allocation size, in bytes.
                                   // This value is accurate for allocations that are less than the length of a ULONG(4,294,967,295 bytes).
                                   // If the allocation is greater, this field contains a truncated value.
                                   // Use AllocationAmount64 for very large allocations.
    uint32_t AllocationKind;       // 0x0 - Small object allocation(allocation is in small object heap).
                                   // 0x1 - Large object allocation(allocation is in large object heap).
    uint16_t ClrInstanceId;        // Unique ID for the instance of CLR or CoreCLR.
    uint64_t AllocationAmount64;   // The allocation size, in bytes.This value is accurate for very large allocations.
    uintptr_t TypeId;              // The address of the MethodTable. When there are several types of objects that were allocated during this event,
                                   // this is the address of the MethodTable that corresponds to the last object allocated (the object that caused the 100 KB threshold to be exceeded).
    const WCHAR* TypeName;         // The name of the type that was allocated. When there are several types of objects that were allocated during this event,
                                   // this is the type of the last object allocated (the object that caused the 100 KB threshold to be exceeded).
    uint32_t HeapIndex;            // The heap where the object was allocated. This value is 0 (zero) when running with workstation garbage collection.
    uintptr_t Address;             // The address of the last allocated object.
    uint64_t ObjectSize;           // The size of the last allocated object.
};

struct ContentionStopV1Payload
{
    uint8_t ContentionFlags;   // 0 for managed; 1 for native.
    uint16_t ClrInstanceId;    // Unique ID for the instance of CLR.
    double_t DurationNs;       // Duration of the contention (without spinning)
};

struct GCStartPayload
{
    uint32_t Count;
    uint32_t Depth;
    uint32_t Reason;
    uint32_t Type;
};

struct GCEndPayload
{
    uint32_t Count;
    uint32_t Depth;
};

struct GCHeapStatsV1Payload
{
    uint64_t GenerationSize0;
    uint64_t TotalPromotedSize0;
    uint64_t GenerationSize1;
    uint64_t TotalPromotedSize1;
    uint64_t GenerationSize2;
    uint64_t TotalPromotedSize2;
    uint64_t GenerationSize3;
    uint64_t TotalPromotedSize3;
    uint64_t FinalizationPromotedSize;
    uint64_t FinalizationPromotedCount;
    uint32_t PinnedObjectCount;
    uint32_t SinkBlockCount;
    uint32_t GCHandleCount;
    uint16_t ClrInstanceID;
};

struct GCGlobalHeapPayload
{
    uint64_t FinalYoungestDesired;
    uint32_t NumHeaps;
    uint32_t CondemnedGeneration;
    uint32_t Gen0ReductionCount;
    uint32_t Reason;
    uint32_t GlobalMechanisms;
};
#pragma pack()

class IContentionListener;


struct GCDetails
{
    int32_t Number;
    uint32_t Generation;
    GCReason Reason;
    GCType Type;
    bool IsCompacting;
    uint64_t PauseDuration;
    uint64_t StartTimestamp;
};

class ClrEventsParser
{
public:
    static const int KEYWORD_GC = 0x1;
    static const int KEYWORD_CONTENTION = 0x4000;

public:
    ClrEventsParser(ICorProfilerInfo12* pCorProfilerInfo,
                    IAllocationsListener* pAllocationListener,
                    IContentionListener* pContentionListener,
                    IGCSuspensionsListener* pGCSuspensionsListener
                    );

    void ParseEvent(EVENTPIPE_PROVIDER provider,
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
                    UINT_PTR stackFrames[]
                    );
    void Register(IGarbageCollectionsListener* pGarbageCollectionsListener);

private:
    bool TryGetEventInfo(LPCBYTE pMetadata, ULONG cbMetadata, WCHAR*& name, DWORD& id, INT64& keywords, DWORD& version);
    void ParseGcEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData);
    void ParseContentionEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData);

    // garbage collection events processing
    void OnGCTriggered();
    void OnGCStart(GCStartPayload& payload);
    void OnGCStop(GCEndPayload& payload);
    void OnGCSuspendEEBegin();
    void OnGCRestartEEEnd();
    void OnGCHeapStats();
    void OnGCGlobalHeapHistory(GCGlobalHeapPayload& payload);
    void NotifySuspension(uint32_t number, uint32_t generation, uint64_t duration, uint64_t timestamp);
    void NotifyGarbageCollectionStarted(int32_t number, uint32_t generation, GCReason reason, GCType type);

    void NotifyGarbageCollectionEnd(
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type,
        bool isCompacting,
        uint64_t pauseDuration,
        uint64_t totalDuration,
        uint64_t endTimestamp
        );
    GCDetails& GetCurrentGC();
    void InitializeGC(GCDetails& gc, GCStartPayload& payload);
    void ClearCollections();
    static void ResetGC(GCDetails& gc);
    static uint64_t GetCurrentTimestamp();


private:
    // Points to the UTF16, null terminated string from the given event data buffer
    // and update the offset accordingly
    static WCHAR* ReadWideString(LPCBYTE eventData, ULONG cbEventData, ULONG* offset)
    {
        WCHAR* start = (WCHAR*)(eventData + *offset);
        size_t length = WStrLen(start);

        // Account for the null character
        *offset += (ULONG)((length + 1) * sizeof(WCHAR));

        assert(*offset <= cbEventData);
        return start;
    }

    template <typename T>
    bool Read(T& value, LPCBYTE eventData, ULONG cbEventData, ULONG& offset)
    {
        if ((offset + sizeof(T)) > cbEventData)
        {
            return false;
        }

        memcpy(&value, (T*)(eventData + offset), sizeof(T));
        offset += sizeof(T);
        return true;
    }

private:
    ICorProfilerInfo12* _pCorProfilerInfo = nullptr;
    IAllocationsListener* _pAllocationListener = nullptr;
    IContentionListener* _pContentionListener = nullptr;
    IGCSuspensionsListener* _pGCSuspensionsListener = nullptr;
    std::vector<IGarbageCollectionsListener*> _pGarbageCollectionsListeners;

 // state for garbage collection details including Stop The World duration
private:
    // set when GCSuspendEEBegin is received (usually no GC is known at that time)
    uint64_t _suspensionStart;

    // for concurrent mode, a background GC could be started
    GCDetails _currentBGC;

    // this is a foreground GC (could be triggered while a background GC is already running)
    GCDetails _gcInProgress;

    // allow EventPipe connection with the CLR of the current process
    std::unique_ptr<DiagnosticsClient> _pDiagnosticClient;
    int32_t _pid;


private:
    const int EVENT_ALLOCATION_TICK = 10;   // version 4 contains the size + reference
    const int EVENT_CONTENTION_STOP = 91;   // version 1 contains the duration in nanoseconds

    // Events emitted during garbage collection lifetime
    // read https://medium.com/criteo-engineering/spying-on-net-garbage-collector-with-net-core-eventpipes-9f2a986d5705?source=friends_link&sk=baf9a7766fb5c7899b781f016803597f
    //  and https://www.codeproject.com/Articles/1127179/Visualising-the-NET-Garbage-Collector
    // for more details
    const int EVENT_GC_TRIGGERED = 35;
    const int EVENT_GC_START = 1;                 // V2
    const int EVENT_GC_END = 2;                   // V1
    const int EVENT_GC_HEAP_STAT = 4;             // V1
    const int EVENT_GC_GLOBAL_HEAP_HISTORY = 205; // V2
    const int EVENT_GC_SUSPEND_EE_BEGIN = 9;      // V1
    const int EVENT_GC_RESTART_EE_END = 3;        // V2
};
