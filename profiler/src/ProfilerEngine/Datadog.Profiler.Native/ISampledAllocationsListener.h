// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawAllocationSample.h"

class ISampledAllocationsListener
{
public:
    virtual void OnAllocation(RawAllocationSample& rawSample) = 0;

    virtual ~ISampledAllocationsListener() = default;
};