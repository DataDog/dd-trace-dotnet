// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <stdint.h>
#include <vector>

#include "IThreadInfo.h"
#include "SampleValueTypeProvider.h"
#include "RawSampleTraits.hpp"

#include "cor.h"
#include "corprof.h"

class Sample;

template <class RealSampleType>
class RawSample
{
public:
    RawSample() noexcept
        :
        Timestamp{0},
        AppDomainId{0},
        LocalRootSpanId{0},
        SpanId{0},
        ThreadInfo{nullptr},
        Stack{}
    {
    }
    virtual ~RawSample() = default;

    RawSample(RawSample const&) = delete;
    RawSample& operator=(RawSample const&) = delete;

    RawSample(RawSample&& other) noexcept
    {
        *this = std::move(other);
    }

    RawSample& operator=(RawSample&& other) noexcept
    {
        Timestamp = other.Timestamp;
        AppDomainId = other.AppDomainId;
        LocalRootSpanId = other.LocalRootSpanId;
        SpanId = other.SpanId;
        ThreadInfo = std::move(other.ThreadInfo);
        Stack = std::move(other.Stack);

        return *this;
    }

    // set values and additional labels on target sample
    virtual void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffset) const = 0;

public:
    std::uint64_t Timestamp; // _unixTimeUtc;
    AppDomainID AppDomainId;
    std::uint64_t LocalRootSpanId; // _localRootSpanId;
    std::uint64_t SpanId;          // _spanId;
    std::shared_ptr<IThreadInfo> ThreadInfo;

    typename RawSampleTraits<RealSampleType>::collection_type Stack;
};