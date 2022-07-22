// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <stdint.h>
#include <vector>
#include "cor.h"
#include "corprof.h"
#include "ManagedThreadInfo.h"


class Sample;

class RawSample
{
public:
    RawSample();
    virtual ~RawSample() = default;

    // set values and additional labels on target sample
    virtual void OnTransform(Sample& sample) const = 0;

public:
    std::uint64_t Timestamp;        // _unixTimeUtc;
    AppDomainID AppDomainId;
    std::uint64_t LocalRootSpanId;  // _localRootSpanId;
    std::uint64_t SpanId;           // _spanId;
    ManagedThreadInfo* ThreadInfo;

    // array of instruction pointers (32 or 64 bit address)
    std::vector<std::uintptr_t> Stack;
};
