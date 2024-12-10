// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSnapshotResultBuffer.h"

#include <chrono>

using namespace std::chrono_literals;

StackSnapshotResultBuffer::StackSnapshotResultBuffer() :
    _unixTimeUtc{0},
    _representedDuration{0},
    _localRootSpanId{0},
    _spanId{0},
    _callstack{},
    _reuseCallstack{false}
{
}

StackSnapshotResultBuffer::~StackSnapshotResultBuffer()
{
    _unixTimeUtc = 0ns;
    _representedDuration = 0ns;
    _localRootSpanId = 0;
    _spanId = 0;
    _reuseCallstack = false;
}

void StackSnapshotResultBuffer::Reset()
{
    _localRootSpanId = 0;
    _spanId = 0;
    _representedDuration = 0ns;
    _unixTimeUtc = 0ns;
    _callstack = {};
    _reuseCallstack = false;
}
