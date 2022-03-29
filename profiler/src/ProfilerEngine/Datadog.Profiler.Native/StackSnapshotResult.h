// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "ManagedThreadInfo.h"
#include "StackFrameInfo.h"
#include "StackSnapshotResultFrameInfo.h"
#include "StackSnapshotResultReusableBuffer.h"

/// <summary>
/// Example byte layout for a 2-frame Snapshot result:
///
///   RepresentedDurationNanoseconds(8 bytes)
///    |
///    |              ProfilerThreadInfoId(8 bytes)
///    |               |              ProfilerAppDomainId(8 bytes)
///    |               |               |             FramesCount(2 bytes)
///    |               |               |               |  CodeKind of Frame #0(1 byte)
///    |               |               |               |   |FrameInfoCode of Frame #0(8 bytes)
///    |               |               |               |   | |               CodeKind of Frame #1(1 byte)
///    |               |               |               |   | |               |FrameInfoCode of Frame #1(8 bytes)
///    |               |               |               |   | |               | |
///    |               |               |               |   | |               | |              Next StackSnapshotResult's RepresentedDurationNanoseconds(8 bytes)
///    ↓               ↓               ↓               ↓   ↓ ↓               ↓ ↓               ↓
///    —–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—
///   |•| | | | | | | |•| | | | | | | |•| | | | | | | |•| |•|•| | | | | | | |•|•| | | | | | | |•| | | |. . . .
///   |0| | | | | | | | | |1| | | | | | | | | |2| | | | | | | | | |3| | | | | | | | | |4| | | | | | | |. . . .
///   |0|1|2|3|4|5|6|7|8|9|0|1|2|3|4|5|6|7|8|9|0|1|2|3|4|5|6|7|8|9|0|1|2|3|4|5|6|7|8|9|0|1|2|3|4|5|6|7|. . . .
///    —–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—
///    ↑                                                                                       ↑
///   _pData                                                                                  Next StackSnapshotResult's _pData
///
/// (Note: FrameInfoCode is the ClrFunctionId OR the NativeIP, depending on codeKind)
///
/// Lengths:
///
///     uint64_t    Get     RepresentedDurationNanoseconds:     64 bits /  8 bytes      Offset:  0 bytes
///     uint64_t    Get     ProfilerThreadInfoId:               64 bits /  8 bytes      Offset:  8 bytes  (* currently, only 4 out of those 8 bytes are used.)
///     uint64_t    Get     ProfilerAppDomainId:                64 bits /  8 bytes      Offset: 16 bytes
///     uint16_t    Get     FramesCount:                        16 bits /  2 bytes      Offset: 24 bytes
///
///     bool        TryGet  FrameAtIndex(uint16_t index, CodeKind* codeKind, uint64_t* frameInfoCode):
///                                                             8+64=72 bits / 1+8=9 bytes per frame
///                                                                                     Offset Frame #0: 26 bytes (CodeKind); 27 bytes (FrameInfoCode)
///                                                                                     Offset Frame #n: 26 + 9n bytes (CodeKind); and 27 + 9n bytes (FrameInfoCode)
///
///
/// Total: Bits: 208 fixed + 72 per frame / Bytes: 26 fixed + 9 per frame.
///
/// A result with 100 frames: 208 + 7200 = 7408 bits
///                           26 + 900 = 926 bytes.
///
/// At 1ms sample rate, this is 0.883 MBytes per thread per second!
///
/// </summary>
struct StackSnapshotResult
{
public:
    static StackSnapshotResult InvalidSnapshot;

    static std::uint32_t GetRequiredBufferBytes(const StackSnapshotResultBuffer* pSnapshotResult);

    StackSnapshotResult() = delete;
    explicit StackSnapshotResult(void* pSnapshotMemory);

    std::uint64_t GetRepresentedDurationNanoseconds(void) const;
    std::uint32_t GetProfilerThreadInfoId(void) const;
    std::uint64_t GetProfilerAppDomainId(void) const;
    std::uint16_t GetFramesCount(void) const;

    // frameInfoCode is the ClrFunctionId OR the NativeIP, depending on codeKind
    bool TryGetFrameAtIndex(std::uint16_t index, StackFrameCodeKind* pCodeKind, std::uint64_t* pFrameInfoCode) const;

    inline bool IsValid(void) const;

    std::uint32_t WriteData(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo);
    std::uint32_t GetUsedBytesCount(void) const;
    inline void* GetNextSnapshotMemoryPointer(void) const;

private:
    inline void* GetPointerToOffset(std::uint32_t offset) const;
    inline static std::uint32_t GetRequiredBufferBytesForFramesCount(std::uint16_t framesCount);

private:
    void* _pData;
};

inline bool StackSnapshotResult::IsValid(void) const
{
    return (_pData != nullptr);
}

inline void* StackSnapshotResult::GetPointerToOffset(std::uint32_t offset) const
{
    if (!IsValid())
    {
        return nullptr;
    }

    typedef unsigned char Byte;
    return (static_cast<Byte*>(_pData) + offset);
}

inline void* StackSnapshotResult::GetNextSnapshotMemoryPointer(void) const
{
    return GetPointerToOffset(GetUsedBytesCount());
}

inline std::uint32_t StackSnapshotResult::GetRequiredBufferBytesForFramesCount(std::uint16_t framesCount)
{
    return 26 + 9 * framesCount;
}
