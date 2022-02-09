// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSnapshotResultReusableBuffer.h"
#include "StackFrameCodeKind.h"

StackSnapshotResultFrameInfo StackSnapshotResultBuffer::UnusableFrameInfo(static_cast<StackFrameCodeKind>(0xFFFF),
                                                                          static_cast<FunctionID>(0xFFFFFFFFFFFFFFFF),
                                                                          static_cast<UINT_PTR>(0xFFFFFFFFFFFFFFFF),
                                                                          static_cast<std::int64_t>(0xFFFFFFFFFFFFFFFF));

StackSnapshotResultBuffer::StackSnapshotResultBuffer(std::uint16_t initialCapacity) :
    _unixTimeUtc{0},
    _representedDurationNanoseconds{0},
    _appDomainId{0},
    _stackFrames{nullptr},
    _currentCapacity{0},
    _nextResetCapacity{initialCapacity},
    _currentFramesCount{0},
    _traceContextTraceId{0},
    _traceContextSpanId{0}
{
}

StackSnapshotResultBuffer::~StackSnapshotResultBuffer()
{
    StackSnapshotResultFrameInfo* stackFrames = _stackFrames;
    if (stackFrames != nullptr)
    {
        delete[] stackFrames;
        _stackFrames = nullptr;
    }

    _unixTimeUtc = 0;
    _representedDurationNanoseconds = 0;
    _appDomainId = static_cast<AppDomainID>(0);
    _currentCapacity = 0;
    _nextResetCapacity = 0;
    _currentFramesCount = 0;
    _traceContextTraceId = 0;
    _traceContextSpanId = 0;
}

void StackSnapshotResultReusableBuffer::Reset(void)
{
    if (_nextResetCapacity != _currentCapacity)
    {
        if (_stackFrames != nullptr)
        {
            delete[] _stackFrames;
        }

        _stackFrames = (_nextResetCapacity > 0) ? new StackSnapshotResultFrameInfo[_nextResetCapacity] : nullptr;
        _currentCapacity = _nextResetCapacity;
    }
    else
    {
        for (std::uint16_t i = 0; i < _currentCapacity; i++)
        {
            (_stackFrames + i)->Reset();
        }
    }

    _traceContextTraceId = 0;
    _traceContextSpanId = 0;

    _currentFramesCount = 0;
    _appDomainId = static_cast<AppDomainID>(0);
    _representedDurationNanoseconds = 0;
    _unixTimeUtc = 0;
}
