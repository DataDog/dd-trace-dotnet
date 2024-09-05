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

#include "../../../../shared/src/native-src/string.h"
#include "assert.h"


// keywords
const int KEYWORD_CONTENTION = 0x00004000;
const int KEYWORD_GC = 0x00000001;
const int KEYWORD_STACKWALK = 0x40000000;

// events id
const int EVENT_CONTENTION_STOP = 91; // version 1 contains the duration in nanoseconds
const int EVENT_CONTENTION_START = 81;

const int EVENT_ALLOCATION_TICK = 10; // version 4 contains the size + reference
const int EVENT_GC_TRIGGERED = 35;
const int EVENT_GC_START = 1;                 // V2
const int EVENT_GC_END = 2;                   // V1
const int EVENT_GC_HEAP_STAT = 4;             // V1
const int EVENT_GC_GLOBAL_HEAP_HISTORY = 205; // V2
const int EVENT_GC_SUSPEND_EE_BEGIN = 9;      // V1
const int EVENT_GC_RESTART_EE_END = 3;        // V2

const int EVENT_GC_JOIN = 203;
const int EVENT_GC_PER_HEAP_HISTORY = 204;
const int EVENT_GC_MARKWITHTYPE = 202;
const int EVENT_GC_PINOBJECTATGCTIME = 33;

const int EVENT_SW_STACK = 82;



#define LONG_LENGTH 1024

#pragma pack(1)
struct StackWalkPayload
{
    uint16_t ClrInstanceID;
    uint8_t Reserved1;
    uint8_t Reserved2;
    uint32_t FrameCount;
    uintptr_t Stack[1];
};

struct AllocationTickV2Payload  // for .NET Framework ???
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
};

// This will not be filled up but represents what is received from the .NET Framework
struct AllocationTickV3Payload  // for .NET Framework 4.8
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
    WCHAR FirstCharInName;         // The name of the type that was allocated. When there are several types of objects that were allocated during this event,
                                   // this is the type of the last object allocated (the object that caused the 100 KB threshold to be exceeded).

    // uint32_t HeapIndex and uintptr_t Address appear AFTER the TypeName
};

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

struct ContentionPayload  // for .NET Framework Contention(Start/Stop) share the same generic payload
{
    uint8_t ContentionFlags;   // 0 for managed; 1 for native.
    uint16_t ClrInstanceId;    // Unique ID for the instance of CLR.
};

struct ContentionStopV1Payload // for .NET Core/ 5+
{
    uint8_t ContentionFlags;   // 0 for managed; 1 for native.
    uint16_t ClrInstanceId;    // Unique ID for the instance of CLR.
    double_t DurationNs;       // Duration of the contention (without spinning)
};

struct ContentionStartV2Payload // for .NET Core/ 5+
{
    uint8_t ContentionFlags; // 0 for managed; 1 for native.
    uint16_t ClrInstanceId;  // Unique ID for the instance of CLR.
    uintptr_t LockId;
    uintptr_t AssociatedObjectID;
    // This is a copy/paste from the CLR, but the LockOwnerThreadID is not a ThreadID but an OS Thread Id
    uint64_t LockOwnerThreadID;
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

struct GCHeapStatsV2Payload
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

    // for POH
    uint64_t GenerationSize4;
    uint64_t TotalPromotedSize4;
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

    uint64_t gen2Size;
    uint64_t lohSize;
    uint64_t pohSize;

    // GlobalHeapHistory and HeapStats events are not received in the same order
    // between Framework and CoreCLR. So we need to keep track of what has been received
    bool HasGlobalHeapHistoryBeenReceived;
    bool HasHeapStatsBeenReceived;
};

class ClrEventsParser
{
public:
    static const int KEYWORD_GC = 0x1;
    static const int KEYWORD_CONTENTION = 0x4000;

public:
    ClrEventsParser(
        IAllocationsListener* pAllocationListener,
        IContentionListener* pContentionListener,
        IGCSuspensionsListener* pGCSuspensionsListener
        );


    // the parser is used both for synchronous (ICorProfilerCallback) and
    // asynchronous (.NET Framework via the Agent) cases. The timestamp parameter
    // is only valid (different from 0) in the asynchronous scenario.
    // As of today, only the GC related events could be received asynchronously.
    //
    // Lock contention and AllocationTick are synchronous only here.
    //
    void ParseEvent(
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        ULONG cbEventData,
        LPCBYTE eventData
        );

    void Register(IGarbageCollectionsListener* pGarbageCollectionsListener);

private:
    void ParseGcEvent(uint64_t timestamp, DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData);
    void ParseContentionEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData);

    // garbage collection events processing
    void OnGCTriggered();
    void OnGCStart(uint64_t timestamp, GCStartPayload& payload);
    void OnGCEnd(GCEndPayload& payload);
    void OnGCSuspendEEBegin(uint64_t timestamp);
    void OnGCRestartEEEnd(uint64_t timestamp);
    void OnGCHeapStats(uint64_t timestamp, uint64_t gen2Size, uint64_t lohSize, uint64_t pohSize);
    void OnGCGlobalHeapHistory(uint64_t timestamp, GCGlobalHeapPayload& payload);
    void NotifySuspension(uint64_t timestamp, uint32_t number, uint32_t generation, uint64_t duration);
    void NotifyGarbageCollectionStarted(uint64_t timestamp, int32_t number, uint32_t generation, GCReason reason, GCType type);

    void NotifyGarbageCollectionEnd(
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type,
        bool isCompacting,
        uint64_t pauseDuration,
        uint64_t totalDuration,
        uint64_t endTimestamp,
        uint64_t gen2Size,
        uint64_t lohSize,
        uint64_t pohSize
        );
    GCDetails& GetCurrentGC();
    void InitializeGC(uint64_t timestamp, GCDetails& gc, GCStartPayload& payload);
    static void ResetGC(GCDetails& gc);
    static uint64_t GetCurrentTimestamp();


public:
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
    static bool Read(T& value, LPCBYTE eventData, ULONG cbEventData, ULONG& offset)
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

    // this is a foreground/non concurrent GC (could be triggered while a background GC is already running)
    GCDetails _gcInProgress;

private:
    const int EVENT_ALLOCATION_TICK = 10;   // version 4 contains the size + reference
    const int EVENT_CONTENTION_STOP = 91;   // version 1 contains the duration in nanoseconds
    const int EVENT_CONTENTION_START = 81; // version 2 contains thread id of the threads that owns the lock

    // Events emitted during garbage collection lifetime
    // read https://medium.com/criteo-engineering/spying-on-net-garbage-collector-with-net-core-eventpipes-9f2a986d5705?source=friends_link&sk=baf9a7766fb5c7899b781f016803597f
    //  and https://www.codeproject.com/Articles/1127179/Visualising-the-NET-Garbage-Collector
    // for more details.
    // The versions payload are supported for both .NET Core and Framework
    const int EVENT_GC_TRIGGERED = 35;
    const int EVENT_GC_START = 1;                 // V2
    const int EVENT_GC_END = 2;                   // V1
    const int EVENT_GC_HEAP_STAT = 4;             // V1
    const int EVENT_GC_GLOBAL_HEAP_HISTORY = 205; // V2
    const int EVENT_GC_SUSPEND_EE_BEGIN = 9;      // V1
    const int EVENT_GC_RESTART_EE_END = 3;        // V2
};
