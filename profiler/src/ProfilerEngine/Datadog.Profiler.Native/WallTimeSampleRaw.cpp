// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "WallTimeSampleRaw.h"


WallTimeSampleRaw::WallTimeSampleRaw()
    :
    Timestamp{0},
    Duration{0},
    AppDomainId{0},
    TraceId{0},
    SpanId{0},
    Stack{},
    ThreadInfo{nullptr}
{
}

