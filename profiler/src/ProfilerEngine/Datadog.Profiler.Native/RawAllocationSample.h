// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawAllocationSample : public RawSample
{
public:
    inline void OnTransform(std::shared_ptr<Sample>& sample, uint32_t valueOffset) const override
    {
        uint32_t allocationCountIndex = valueOffset;
        uint32_t allocationSizeIndex = valueOffset + 1;

        sample->AddValue(1, allocationCountIndex);
        sample->AddValue(AllocationSize, allocationSizeIndex);
        sample->AddLabel(Label(Sample::AllocationClassLabel, AllocationClass));
    }

    std::string AllocationClass;
    int64_t AllocationSize;
    uintptr_t Address;
    ClassID MethodTable;
};