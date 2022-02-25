// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <cstdint>
#include <vector>
#include "cor.h"
#include "corprof.h"
#include "ManagedThreadInfo.h"


class WallTimeSampleRaw
{
public:
    WallTimeSampleRaw();
    // no need to define a move-operator because it would be equivalent to the compiler-generated copy constructor
    // i.e. no field contains deep copiable object (it would have been different if vector<string> for example

public:
    std::uint64_t  Timestamp;   // _unixTimeUtc;
    std::uint64_t  Duration;    // _representedDurationNanoseconds;
    AppDomainID    AppDomainId;
    std::uint64_t  TraceId;     // _traceContextTraceId;
    std::uint64_t  SpanId;      // _traceContextSpanId;
    ManagedThreadInfo* ThreadInfo;

    // array of instruction pointers (32 or 64 bit address)
    std::vector<std::uintptr_t> Stack;
};
