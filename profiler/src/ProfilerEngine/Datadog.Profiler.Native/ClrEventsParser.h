// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <string>
#include <map>
#include <shared_mutex>

#include "shared/src/native-src/string.h"
#include "assert.h"


#define LONG_LENGTH 1024

// from https://github.com/dotnet/samples/blob/main/core/profiling/eventpipe/src/CorProfiler.h
template <class Key, class Value>
class ThreadSafeMap
{
private:
    std::map<Key, Value> _map;
    mutable std::shared_mutex _mutex;

public:
    typename std::map<Key, Value>::const_iterator find(Key key) const
    {
        std::shared_lock lock(_mutex);
        return _map.find(key);
    }

    typename std::map<Key, Value>::const_iterator end() const
    {
        return _map.end();
    }

    // Returns true if new value was inserted
    bool insertNew(Key key, Value value)
    {
        std::unique_lock lock(_mutex);

        if (_map.find(key) != _map.end())
        {
            return false;
        }

        _map[key] = value;
        return true;
    }
};

struct EventPipeMetadataInstance
{
    UINT32 id;
    LPWSTR name;
    INT64 keywords;
    UINT32 level;
    UINT32 version;
    // BYTE opcode;
    // Don't try to get the format of the payload because we already now which events we are interested in.
    // See the xxxPayload types below.
};

struct AllocationTickV4Payload
{
    std::uint32_t AllocationAmount;     // The allocation size, in bytes.
                                        // This value is accurate for allocations that are less than the length of a ULONG(4,294,967,295 bytes).
                                        // If the allocation is greater, this field contains a truncated value.
                                        // Use AllocationAmount64 for very large allocations.
    std::uint32_t AllocationKind;       // 0x0 - Small object allocation(allocation is in small object heap).
                                        // 0x1 - Large object allocation(allocation is in large object heap).
    std::uint16_t ClrInstanceId;        // Unique ID for the instance of CLR or CoreCLR.
    std::uint64_t AllocationAmount64;   // The allocation size, in bytes.This value is accurate for very large allocations.
    std::uintptr_t TypeId;              // The address of the MethodTable. When there are several types of objects that were allocated during this event,
                                        // this is the address of the MethodTable that corresponds to the last object allocated (the object that caused the 100 KB threshold to be exceeded).
    const WCHAR* TypeName;              // The name of the type that was allocated. When there are several types of objects that were allocated during this event,
                                        // this is the type of the last object allocated (the object that caused the 100 KB threshold to be exceeded).
    std::uint32_t HeapIndex;            // The heap where the object was allocated. This value is 0 (zero) when running with workstation garbage collection.
    std::uintptr_t Address;             // The address of the last allocated object.
    std::uint64_t ObjectSize;           // The size of the last allocated object.
};

struct ContentionStopV1Payload
{
    std::uint8_t ContentionFlags;   // 0 for managed; 1 for native.
    std::uint16_t ClrInstanceId;    // Unique ID for the instance of CLR.
    std::double_t DurationNs;       // Duration of the contention (without spinning)
};



class ClrEventsParser
{
public:
    ClrEventsParser(ICorProfilerInfo12* pCorProfilerInfo);
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

private:
    std::string GetProviderName(EVENTPIPE_PROVIDER provider);
    EventPipeMetadataInstance GetMetadata(LPCBYTE pMetadata, ULONG cbMetadata);
    void ParseGcEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData);
    void ParseContentionEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData);

private:
    // Points to the UTF16, null terminated string from the given event data buffer
    // and update the offset accordingly
    WCHAR* ReadWideString(LPCBYTE eventData, ULONG cbEventData, ULONG* offset)
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
    ThreadSafeMap<EVENTPIPE_PROVIDER, std::string> _providerNameCache;
    ThreadSafeMap<LPCBYTE, EventPipeMetadataInstance> _metadataCache;

private:
    const int KEYWORD_GC = 0x1;
    const int KEYWORD_CONTENTION = 0x4000;

    const int EVENT_ALLOCATION_TICK = 10;   // version 4 contains the size + reference
    const int EVENT_CONTENTION_STOP = 91;   // version 1 contains the duration in nanoseconds
};
