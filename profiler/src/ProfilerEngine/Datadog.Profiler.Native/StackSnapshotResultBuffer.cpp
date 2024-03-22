// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSnapshotResultBuffer.h"

StackSnapshotResultBuffer::StackSnapshotResultBuffer() :
    _instructionPointers{},
    _currentFramesCount{0},
    _localRootSpanId{nullptr},
    _spanId{nullptr},
    _internalIps{}
{
    _instructionPointers = shared::span(_internalIps.data(), _internalIps.size());
}

StackSnapshotResultBuffer::~StackSnapshotResultBuffer()
{
    _currentFramesCount = 0;
    _localRootSpanId = nullptr;
    _spanId = nullptr;
}

void StackSnapshotResultBuffer::Reset()
{
    _localRootSpanId = nullptr;
    _spanId = nullptr;

    _instructionPointers = shared::span(_internalIps.data(), _internalIps.size());
    _currentFramesCount = 0;
}
