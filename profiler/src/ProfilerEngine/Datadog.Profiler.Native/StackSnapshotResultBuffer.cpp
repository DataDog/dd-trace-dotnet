// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSnapshotResultBuffer.h"

StackSnapshotResultBuffer::StackSnapshotResultBuffer() :
    _unixTimeUtc{0},
    _representedDurationNanoseconds{0},
    _appDomainId{0},
    _instructionPointers{},
    _currentFramesCount{0},
    _localRootSpanId{0},
    _spanId{0}
{
}

StackSnapshotResultBuffer::~StackSnapshotResultBuffer()
{
    _unixTimeUtc = 0;
    _representedDurationNanoseconds = 0;
    _appDomainId = static_cast<AppDomainID>(0);
    _currentFramesCount = 0;
    _localRootSpanId = 0;
    _spanId = 0;
}

void StackSnapshotResultBuffer::Reset()
{
    _localRootSpanId = 0;
    _spanId = 0;

    _currentFramesCount = 0;
    _appDomainId = static_cast<AppDomainID>(0);
    _representedDurationNanoseconds = 0;
    _unixTimeUtc = 0;
}
