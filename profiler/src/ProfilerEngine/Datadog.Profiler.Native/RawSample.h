// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <stdint.h>
#include <memory>
#include <vector>

#include "cor.h"
#include "corprof.h"

#include "Callstack.h"
#include "IThreadInfo.h"
#include "SampleValueTypeProvider.h"
#include "SymbolsStore.h"

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
    virtual void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffset, libdatadog::SymbolsStore* symbolsStore) const = 0;

public:
    std::chrono::nanoseconds Timestamp;
    AppDomainID AppDomainId;
    std::uint64_t LocalRootSpanId;  // _localRootSpanId;
    std::uint64_t SpanId;           // _spanId;
    std::shared_ptr<IThreadInfo> ThreadInfo;

    // array of instruction pointers (32 or 64 bit address)
    Callstack Stack;
};
