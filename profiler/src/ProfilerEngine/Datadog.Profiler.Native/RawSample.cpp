// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RawSample.h"

RawSample::RawSample(int32_t index) noexcept
    :
    Timestamp {0},
    AppDomainId {0},
    LocalRootSpanId {0},
    SpanId {0},
    ThreadInfo{nullptr},
    Stack{},
    Index{index}
{
}

RawSample::RawSample(RawSample&& other) noexcept
{
    *this = std::move(other);
}

RawSample& RawSample::operator=(RawSample&& other) noexcept
{
    Timestamp = other.Timestamp;
    AppDomainId = other.AppDomainId;
    LocalRootSpanId = other.LocalRootSpanId;
    SpanId = other.SpanId;
    ThreadInfo = std::move(other.ThreadInfo);
    Stack = std::move(other.Stack);
    Index = other.Index;

    return *this;
}