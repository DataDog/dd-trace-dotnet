// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <stdint.h>
#include <memory>
#include <vector>

#include "cor.h"
#include "corprof.h"
#include "ManagedThreadInfo.h"


class Sample;

class RawSample
{
public:
    RawSample() noexcept;
    virtual ~RawSample() = default;

    RawSample(RawSample const&) = delete;
    RawSample& operator=(RawSample const&) = delete;

    RawSample(RawSample&& other) noexcept;
    RawSample& operator=(RawSample&& other) noexcept;

    // set values and additional labels on target sample
    virtual void OnTransform(std::shared_ptr<Sample>& sample, uint32_t valueOffset) const = 0;

public:
    std::uint64_t Timestamp;        // _unixTimeUtc;
    AppDomainID AppDomainId;
    std::uint64_t LocalRootSpanId;  // _localRootSpanId;
    std::uint64_t SpanId;           // _spanId;
    std::shared_ptr<ManagedThreadInfo> ThreadInfo;

    // array of instruction pointers (32 or 64 bit address)
    std::vector<std::uintptr_t> Stack;
};
