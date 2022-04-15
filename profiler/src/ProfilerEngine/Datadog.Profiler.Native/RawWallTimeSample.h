// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <cstdint>
#include <vector>
#include "cor.h"
#include "corprof.h"
#include "ManagedThreadInfo.h"
#include "RawSample.h"


class RawWallTimeSample : public RawSample
{
public:
    std::uint64_t  Duration;  // in nanoseconds
};
