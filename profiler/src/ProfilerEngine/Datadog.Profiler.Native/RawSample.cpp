// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RawSample.h"

RawSample::RawSample()
    :
    Timestamp {0},
    AppDomainId {0},
    LocalRootSpanId {0},
    SpanId {0},
    ThreadInfo{nullptr},
    Stack{}
{
}