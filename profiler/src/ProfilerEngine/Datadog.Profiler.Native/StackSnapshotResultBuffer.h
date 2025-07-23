// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <array>
#include <chrono>
#include <vector>
#include <cstdint>
#include <utility>

#include "Callstack.h"

#include "shared/src/native-src/dd_span.hpp"

/// <summary>
/// Allocating when a thread is suspended can lead to deadlocks.
/// This container holds a buffer that is used while walking stacks to temporarily hold results.
/// If we wanted to walk stacks of more than one thread concurrently, there would need to be more then once instance of this class.
/// However, since StackFrameCollector implementations currently all walk one stack at a time, they use one instance of this class each.
/// </summary>
class StackSnapshotResultBuffer
{
public:
    inline std::chrono::nanoseconds GetUnixTimeUtc() const;
    inline std::chrono::nanoseconds SetUnixTimeUtc(std::chrono::nanoseconds value);

    inline std::chrono::nanoseconds GetRepresentedDuration() const;
    inline std::chrono::nanoseconds SetRepresentedDuration(std::chrono::nanoseconds value);

    inline std::uint64_t GetLocalRootSpanId() const;
    inline std::uint64_t SetLocalRootSpanId(std::uint64_t value);

    inline std::uint64_t GetSpanId() const;
    inline std::uint64_t SetSpanId(std::uint64_t value);

    inline std::size_t GetFramesCount() const;
    inline void SetFramesCount(std::uint16_t count);

    void Reset();

    inline bool AddFrame(std::uintptr_t ip);
    inline bool AddFakeFrame();

    inline shared::span<uintptr_t> Data();
    inline Callstack GetCallstack();
    inline void SetCallstack(Callstack callstack);

    StackSnapshotResultBuffer();
    ~StackSnapshotResultBuffer();

protected:

    std::chrono::nanoseconds _unixTimeUtc;
    std::chrono::nanoseconds _representedDuration;
    Callstack _callstack;

    std::uint64_t _localRootSpanId;
    std::uint64_t _spanId;
};

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

inline std::chrono::nanoseconds StackSnapshotResultBuffer::GetUnixTimeUtc() const
{
    return _unixTimeUtc;
}

inline std::chrono::nanoseconds StackSnapshotResultBuffer::SetUnixTimeUtc(std::chrono::nanoseconds value)
{
    auto prevValue = _unixTimeUtc;
    _unixTimeUtc = value;
    return prevValue;
}

inline std::chrono::nanoseconds StackSnapshotResultBuffer::GetRepresentedDuration() const
{
    return _representedDuration;
}

inline std::chrono::nanoseconds StackSnapshotResultBuffer::SetRepresentedDuration(std::chrono::nanoseconds value)
{
    auto prevValue = _representedDuration;
    _representedDuration = value;
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
    return _callstack.Size();
}

inline void StackSnapshotResultBuffer::SetFramesCount(std::uint16_t count)
{
    _callstack.SetCount(count);
}

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

inline bool StackSnapshotResultBuffer::AddFrame(std::uintptr_t ip)
{
    return _callstack.Add(ip);
}

inline bool StackSnapshotResultBuffer::AddFakeFrame()
{
    return AddFrame(0);
}

inline shared::span<uintptr_t> StackSnapshotResultBuffer::Data()
{
    return _callstack.AsSpan();
}

inline Callstack StackSnapshotResultBuffer::GetCallstack()
{
    return std::exchange(_callstack, {});
}

inline void StackSnapshotResultBuffer::SetCallstack(Callstack callstack)
{
    _callstack = std::move(callstack);
}
