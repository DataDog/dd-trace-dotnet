#pragma once

#include <stdint.h>

// from FastSerialization.Tag
enum NettraceTag : uint8_t
{
    // from format spec - https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
    NullReference = 1,
    BeginPrivateObject = 5,
    EndObject = 6,

    Error = 0,
    ObjectReference = 2,
    ForwardReference = 3,
    BeginObject = 4,
    ForwardDefinition = 7,
    Byte = 8,
    Int16 = 9,
    Int32 = 10,
    Int64 = 11,
    SkipRegion = 12,
    String = 13,
    Blob = 14,
    Limit = 15
};

enum class ObjectType : uint8_t
{
    Unknown = 0,
    Trace,
    EventBlock,
    MetadataBlock,
    StackBlock,
    SequencePointBlock,
};

#pragma pack(1)
struct ObjectHeader
{
    NettraceTag TagTraceObject;         // 5
    NettraceTag TagTypeObjectForTrace;  // 5
    NettraceTag TagType;                // 1
    uint32_t Version;                   //
    uint32_t MinReaderVersion;          //
    uint32_t NameLength;                // length of UTF8 name that follows
};


// filled up by EventPipeEventSource.FromStream(Deserializer)
#pragma pack(1)
struct ObjectFields
{
    uint16_t Year;
    uint16_t Month;
    uint16_t DayOfWeek;
    uint16_t Day;
    uint16_t Hour;
    uint16_t Minute;
    uint16_t Second;
    uint16_t Millisecond;
    uint64_t SyncTimeQPC;
    uint64_t QPCFrequency;
    uint32_t PointerSize;
    uint32_t ProcessId;
    uint32_t NumProcessors;
    uint32_t ExpectedCPUSamplingRate;
};


// from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
#pragma pack(1)
struct EventBlockHeader
{
    uint16_t HeaderSize;
    uint16_t Flags;
    uint64_t MinTimestamp;
    uint64_t MaxTimestamp;

    // some optional reserved space might be following
};


// from https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeFormat.md
//
#pragma pack(1)
struct EventBlobHeader_V3
{
    uint32_t EventSize;
    uint32_t MetaDataId;
    uint32_t ThreadId;
    uint64_t TimeStamp;
    GUID ActivityID;
    GUID RelatedActivityID;
    uint32_t PayloadSize;
};

#pragma pack(1)
struct EventBlobHeader_V4
{
    uint32_t EventSize;
    uint32_t MetadataId;
    uint32_t SequenceNumber;
    uint64_t ThreadId;
    uint64_t CaptureThreadId;
    uint32_t ProcessorNumber;
    uint32_t StackId;
    uint64_t Timestamp;
    GUID ActivityId;
    GUID RelatedActivityId;
    uint32_t PayloadSize;
};

struct EventBlobHeader : EventBlobHeader_V4
{
    bool IsSorted;
    uint32_t PayloadSize;
    uint32_t HeaderSize;
    uint32_t TotalNonHeaderSize;
};


// look at:
//  Microsoft.Diagnostics.Tracing.EventPipeEventHeader.ReadFromFormatV4()
//  https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/EventPipeEventSource.cs
enum CompressedHeaderFlags
{
    MetadataId                  = 1 << 0,
    CaptureThreadAndSequence    = 1 << 1,
    ThreadId                    = 1 << 2,
    StackId                     = 1 << 3,
    ActivityId                  = 1 << 4,
    RelatedActivityId           = 1 << 5,
    Sorted                      = 1 << 6,
    DataLength                  = 1 << 7
};
