// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"

class RawCpuSample : public RawSample
{
public:
    std::uint64_t Duration;  // in milliseconds
};
