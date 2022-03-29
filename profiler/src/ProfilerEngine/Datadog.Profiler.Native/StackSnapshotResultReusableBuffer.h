// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "StackFrameInfo.h"
#include "StackSnapshotResultFrameInfo.h"

/// <summary>
/// Allocating when a thread is suspended can lead to deadlocks.
/// This container holds a buffer that is used while walking stacks to temporarily hold results.
/// If we wanted to walk stacks of more than one thread concurrently, there would need to be more then once instance of this class.
/// However, since StackFrameCollector implementations currently all walk one stack at a time, they use one instance of this class each.
/// </summary>
class StackSnapshotResultBuffer
{
public:
    StackSnapshotResultBuffer() = delete;

    inline std::uint64_t GetUnixTimeUtc(void) const;
    inline std::uint64_t SetUnixTimeUtc(std::uint64_t value);

    inline std::uint64_t GetRepresentedDurationNanoseconds(void) const;
    inline std::uint64_t SetRepresentedDurationNanoseconds(std::uint64_t value);

    inline AppDomainID GetAppDomainId(void) const;
    inline AppDomainID SetAppDomainId(AppDomainID appDomainId);

    inline std::uint64_t GetTraceContextTraceId(void) const;
    inline std::uint64_t SetTraceContextTraceId(std::uint64_t value);

    inline std::uint64_t GetTraceContextSpanId(void) const;
    inline std::uint64_t SetTraceContextSpanId(std::uint64_t value);

    inline std::uint16_t GetFramesCount(void) const;
    inline StackSnapshotResultFrameInfo& GetFrameAtIndex(std::uint16_t index) const;
    inline void CopyInstructionPointers(std::vector<std::uintptr_t>& ips) const;

protected:
    explicit StackSnapshotResultBuffer(std::uint16_t initialCapacity);
    virtual ~StackSnapshotResultBuffer();

protected:
    static StackSnapshotResultFrameInfo UnusableFrameInfo;

    std::uint64_t _unixTimeUtc;
    std::uint64_t _representedDurationNanoseconds;
    AppDomainID _appDomainId;
    StackSnapshotResultFrameInfo* _stackFrames;
    std::uint16_t _currentCapacity;
    std::uint16_t _nextResetCapacity;
    std::uint16_t _currentFramesCount;

    std::uint64_t _traceContextTraceId;
    std::uint64_t _traceContextSpanId;
};

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

class StackSnapshotResultReusableBuffer : public StackSnapshotResultBuffer
{
private:
    static constexpr std::uint16_t MaxSnapshotStackDepth_Initial = 32;
    static constexpr std::uint16_t MaxSnapshotStackDepth_IncStep = 16;
    static constexpr std::uint16_t MaxSnapshotStackDepth_Limit = 2048;

public:
    StackSnapshotResultReusableBuffer() :
        StackSnapshotResultBuffer(MaxSnapshotStackDepth_Initial)
    {
    }
    ~StackSnapshotResultReusableBuffer() override = default;

    inline std::uint16_t GetCapacity();
    inline std::uint16_t GrowCapacityAtNextReset();

    void Reset(void);

    inline bool TryAddNextFrame(StackSnapshotResultFrameInfo** frameInfo, bool* hasCapacityForSubsequentFrames);
    inline StackSnapshotResultFrameInfo& GetCurrentFrame(void) const;
};

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

inline std::uint64_t StackSnapshotResultBuffer::GetUnixTimeUtc(void) const
{
    return _unixTimeUtc;
}

inline std::uint64_t StackSnapshotResultBuffer::SetUnixTimeUtc(std::uint64_t value)
{
    std::uint64_t prevValue = _unixTimeUtc;
    _unixTimeUtc = value;
    return prevValue;
}

inline std::uint64_t StackSnapshotResultBuffer::GetRepresentedDurationNanoseconds(void) const
{
    return _representedDurationNanoseconds;
}

inline std::uint64_t StackSnapshotResultBuffer::SetRepresentedDurationNanoseconds(std::uint64_t value)
{
    std::uint64_t prevValue = _representedDurationNanoseconds;
    _representedDurationNanoseconds = value;
    return prevValue;
}

inline AppDomainID StackSnapshotResultBuffer::GetAppDomainId(void) const
{
    return _appDomainId;
}

inline AppDomainID StackSnapshotResultBuffer::SetAppDomainId(AppDomainID value)
{
    AppDomainID prevValue = _appDomainId;
    _appDomainId = value;
    return prevValue;
}

inline std::uint64_t StackSnapshotResultBuffer::GetTraceContextTraceId(void) const
{
    return _traceContextTraceId;
}

inline std::uint64_t StackSnapshotResultBuffer::SetTraceContextTraceId(std::uint64_t value)
{
    std::uint64_t prevValue = _traceContextTraceId;
    _traceContextTraceId = value;
    return prevValue;
}

inline std::uint64_t StackSnapshotResultBuffer::GetTraceContextSpanId(void) const
{
    return _traceContextSpanId;
}

inline std::uint64_t StackSnapshotResultBuffer::SetTraceContextSpanId(std::uint64_t value)
{
    std::uint64_t prevValue = _traceContextSpanId;
    _traceContextTraceId = value;
    return prevValue;
}

inline std::uint16_t StackSnapshotResultBuffer::GetFramesCount(void) const
{
    return _currentFramesCount;
}

inline StackSnapshotResultFrameInfo& StackSnapshotResultBuffer::GetFrameAtIndex(std::uint16_t index) const
{
    if (index >= _currentFramesCount || _stackFrames == nullptr)
    {
        StackSnapshotResultReusableBuffer::UnusableFrameInfo = StackSnapshotResultFrameInfo(static_cast<StackFrameCodeKind>(0xFFFF),
                                                                                            static_cast<FunctionID>(0xFFFFFFFFFFFFFFFF),
                                                                                            static_cast<UINT_PTR>(0xFFFFFFFFFFFFFFFF),
                                                                                            static_cast<std::uint64_t>(0xFFFFFFFFFFFFFFFF));
        return UnusableFrameInfo;
    }

    return *(_stackFrames + index);
}

inline void StackSnapshotResultBuffer::CopyInstructionPointers(std::vector<std::uintptr_t>& ips) const
{
    ips.reserve(_currentFramesCount);
    for (size_t i = 0; i < _currentFramesCount; i++)
    {
        ips.push_back(_stackFrames[i].GetNativeIP());
    }
}

// ----------- ----------- ----------- ----------- ----------- ----------- ----------- ----------- -----------

inline std::uint16_t StackSnapshotResultReusableBuffer::GetCapacity()
{
    return _currentCapacity;
}

inline std::uint16_t StackSnapshotResultReusableBuffer::GrowCapacityAtNextReset()
{
    _nextResetCapacity = _currentCapacity + StackSnapshotResultReusableBuffer::MaxSnapshotStackDepth_IncStep;
    if (_nextResetCapacity > StackSnapshotResultReusableBuffer::MaxSnapshotStackDepth_Limit)
    {
        _nextResetCapacity = StackSnapshotResultReusableBuffer::MaxSnapshotStackDepth_Limit;
    }

    return _nextResetCapacity;
}

inline bool StackSnapshotResultReusableBuffer::TryAddNextFrame(StackSnapshotResultFrameInfo** frameInfo, bool* hasCapacityForSubsequentFrames)
{
    if (_currentFramesCount >= _currentCapacity)
    {
        return false;
    }

    if (frameInfo != nullptr)
    {

        *frameInfo = (_stackFrames + _currentFramesCount);
    }

    _currentFramesCount++;

    if (hasCapacityForSubsequentFrames != nullptr)
    {

        *hasCapacityForSubsequentFrames = (_currentFramesCount < _currentCapacity);
    }

    return true;
}

inline StackSnapshotResultFrameInfo& StackSnapshotResultReusableBuffer::GetCurrentFrame(void) const
{
    return GetFrameAtIndex(_currentFramesCount - 1);
}
