// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <array>
#include <vector>
#include <cstdint>

/// <summary>
/// Allocating when a thread is suspended can lead to deadlocks.
/// This container holds a buffer that is used while walking stacks to temporarily hold results.
/// If we wanted to walk stacks of more than one thread concurrently, there would need to be more then once instance of this class.
/// However, since StackFrameCollector implementations currently all walk one stack at a time, they use one instance of this class each.
/// </summary>
class StackSnapshotResultBuffer
{
public:
    static constexpr std::uint16_t MaxSnapshotStackDepth_Limit = 2049;

    inline std::uint64_t GetUnixTimeUtc() const;
    inline std::uint64_t SetUnixTimeUtc(std::uint64_t value);

    inline std::uint64_t GetRepresentedDurationNanoseconds() const;
    inline std::uint64_t SetRepresentedDurationNanoseconds(std::uint64_t value);

    inline std::uint64_t GetLocalRootSpanId() const;
    inline std::uint64_t SetLocalRootSpanId(std::uint64_t value);

    inline std::uint64_t GetSpanId() const;
    inline std::uint64_t SetSpanId(std::uint64_t value);

    inline std::size_t GetFramesCount() const;
    inline void SetFramesCount(std::uint16_t count);
    inline void CopyInstructionPointers(std::vector<std::uintptr_t>& ips) const;

    void Reset();

    inline bool AddFrame(std::uintptr_t ip);
    inline bool AddFakeFrame();

    inline uintptr_t* Data();

    StackSnapshotResultBuffer();
    ~StackSnapshotResultBuffer();

protected:

    std::uint64_t _unixTimeUtc;
    std::uint64_t _representedDurationNanoseconds;
    std::array<uintptr_t, MaxSnapshotStackDepth_Limit> _instructionPointers;
    std::uint16_t _currentFramesCount;

    std::uint64_t _localRootSpanId;
    std::uint64_t _spanId;
};

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

inline std::uint64_t StackSnapshotResultBuffer::GetUnixTimeUtc() const
{
    return _unixTimeUtc;
}

inline std::uint64_t StackSnapshotResultBuffer::SetUnixTimeUtc(std::uint64_t value)
{
    std::uint64_t prevValue = _unixTimeUtc;
    _unixTimeUtc = value;
    return prevValue;
}

inline std::uint64_t StackSnapshotResultBuffer::GetRepresentedDurationNanoseconds() const
{
    return _representedDurationNanoseconds;
}

inline std::uint64_t StackSnapshotResultBuffer::SetRepresentedDurationNanoseconds(std::uint64_t value)
{
    std::uint64_t prevValue = _representedDurationNanoseconds;
    _representedDurationNanoseconds = value;
    return prevValue;
}

inline std::uint64_t StackSnapshotResultBuffer::GetLocalRootSpanId() const
{
    return _localRootSpanId;
}

inline std::uint64_t StackSnapshotResultBuffer::SetLocalRootSpanId(std::uint64_t value)
{
    std::uint64_t prevValue = _localRootSpanId;
    _localRootSpanId = value;
    return prevValue;
}

inline std::uint64_t StackSnapshotResultBuffer::GetSpanId() const
{
    return _spanId;
}

inline std::uint64_t StackSnapshotResultBuffer::SetSpanId(std::uint64_t value)
{
    std::uint64_t prevValue = _spanId;
    _spanId = value;
    return prevValue;
}

inline std::size_t StackSnapshotResultBuffer::GetFramesCount() const
{
    return _currentFramesCount;
}

inline void StackSnapshotResultBuffer::SetFramesCount(std::uint16_t count)
{
    _currentFramesCount = count;
}

inline void StackSnapshotResultBuffer::CopyInstructionPointers(std::vector<std::uintptr_t>& ips) const
{
    ips.reserve(_currentFramesCount);

    // copy the instruction pointer to the out-parameter
    ips.insert(ips.end(), _instructionPointers.begin(), _instructionPointers.begin() + _currentFramesCount);
}

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

inline bool StackSnapshotResultBuffer::AddFrame(std::uintptr_t ip)
{
    if (_currentFramesCount >= MaxSnapshotStackDepth_Limit)
    {
        return false;
    }

    if (_currentFramesCount == MaxSnapshotStackDepth_Limit - 1)
    {
        _instructionPointers[_currentFramesCount++] = 0;
        return false;
    }

    _instructionPointers[_currentFramesCount++] = ip;
    return true;
}

inline bool StackSnapshotResultBuffer::AddFakeFrame()
{
    return AddFrame(0);
}

inline uintptr_t* StackSnapshotResultBuffer::Data()
{
    return _instructionPointers.data();
}
