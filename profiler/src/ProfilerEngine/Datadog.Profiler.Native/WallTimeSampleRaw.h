// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <cstdint>
#include <vector>
#include "cor.h"
#include "corprof.h"
#include "ManagedThreadInfo.h"
#include "RawSample.h"


class WallTimeSampleRaw : public RawSample
{
public:
    WallTimeSampleRaw();
    // no need to define a move-operator because it would be equivalent to the compiler-generated copy constructor
    // i.e. no field contains deep copiable object (it would have been different if vector<string> for example

public:
    std::uint64_t  Duration;    // _representedDurationNanoseconds;
};
