// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstddef>

#include "ManagedThreadInfo.h"
#include "PInvoke.h"
#include "StackSnapshotResult.h"
#include "StackSnapshotResultReusableBuffer.h"

class StackSnapshotsBufferSegment : public RefCountingObject
{
public:
    StackSnapshotsBufferSegment() = delete;
    explicit StackSnapshotsBufferSegment(std::uint32_t size);
    ~StackSnapshotsBufferSegment() override;
    StackSnapshotsBufferSegment(StackSnapshotsBufferSegment const&) = delete;
    StackSnapshotsBufferSegment& operator=(StackSnapshotsBufferSegment const&) = delete;

    bool TryAddSnapshot(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo);
    bool TryCompleteAndEnqueueForExport(void);
    bool TryResetForWrite(void);
    StackSnapshotResult ReadFirstSnapshot(void) const;
    StackSnapshotResult ReadNextSnapshot(StackSnapshotResult stackSnapshot) const;

    inline std::uint32_t GetTotalSizeInBytes() const;
    inline std::uint32_t GetSnapshotsCount() const;
    inline std::uint64_t GetStartTime() const;
    inline std::uint64_t GetEndTime() const;
    void DebugDumpAllSnapshots(void) const;

private:
    inline void* GetPointerToOffset(std::uint32_t offset) const;
    inline std::uint32_t GetOffsetOfPointer(void* pSnapshotMemory) const;

private:
    static const std::uint32_t InvalidOffsetMarker = 0xFFFFFFFF;
    static const std::uint32_t MaxSegmentSize = 0x7FFFFFFE; // 2GB - 1 byte

    std::mutex _modificationLock;

    void* _segmentBytes;
    std::uint32_t _segmentSize;
    std::uint32_t _nextFreeByteOffset;
    std::uint32_t _snapshotsCount;
    std::uint64_t _unixTimeUtcRangeStart;
    std::uint64_t _unixTimeUtcRangeEnd;
    std::atomic<std::uint8_t> _status;

public:
    enum class StatusCode : std::uint8_t
    {
        Unknown = 0,
        IsAvailableForWrite = 2,
        IsReservedForRead = 3,
        IsFull = 4,
    };

    inline StackSnapshotsBufferSegment::StatusCode GetStatus(void) const;

private:
    inline StackSnapshotsBufferSegment::StatusCode SetStatus(StackSnapshotsBufferSegment::StatusCode newStatus);
};

std::uint32_t StackSnapshotsBufferSegment::GetTotalSizeInBytes() const
{
    return _segmentSize;
}

inline std::uint32_t StackSnapshotsBufferSegment::GetSnapshotsCount() const
{
    return _snapshotsCount;
}

inline std::uint64_t StackSnapshotsBufferSegment::GetStartTime() const
{
    return _unixTimeUtcRangeStart;
}

inline std::uint64_t StackSnapshotsBufferSegment::GetEndTime() const
{
    return _unixTimeUtcRangeEnd;
}

inline void* StackSnapshotsBufferSegment::GetPointerToOffset(std::uint32_t offset) const
{
    if (offset >= _segmentSize)
    {
        return nullptr;
    }

    typedef unsigned char Byte;
    return (static_cast<Byte*>(_segmentBytes) + offset);
}

inline std::uint32_t StackSnapshotsBufferSegment::GetOffsetOfPointer(void* pSnapshotMemory) const
{
    if (_segmentSize == 0 || _segmentBytes == nullptr || pSnapshotMemory == nullptr)
    {
        return StackSnapshotsBufferSegment::InvalidOffsetMarker;
    }

    typedef unsigned char Byte;
    std::int64_t offset = (static_cast<Byte*>(pSnapshotMemory) - static_cast<Byte*>(_segmentBytes));

    return (offset < 0 || StackSnapshotsBufferSegment::MaxSegmentSize < offset)
               ? StackSnapshotsBufferSegment::InvalidOffsetMarker
               : static_cast<std::uint32_t>(offset);
}

inline StackSnapshotsBufferSegment::StatusCode StackSnapshotsBufferSegment::GetStatus(void) const
{
    std::uint8_t code = _status.load();
    return static_cast<StackSnapshotsBufferSegment::StatusCode>(code);
}

inline StackSnapshotsBufferSegment::StatusCode StackSnapshotsBufferSegment::SetStatus(StackSnapshotsBufferSegment::StatusCode newStatus)
{
    std::uint8_t prevCode = _status.exchange(static_cast<std::uint8_t>(newStatus));
    return static_cast<StackSnapshotsBufferSegment::StatusCode>(prevCode);
}
