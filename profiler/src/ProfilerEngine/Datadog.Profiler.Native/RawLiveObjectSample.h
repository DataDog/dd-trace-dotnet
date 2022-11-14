// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawAllocationSample.h"
#include "Sample.h"

class RawLiveObjectSample : public RawAllocationSample
{
public:
    inline void OnTransform(Sample& sample, uint32_t valueOffset) const override
    {
        // take care of allocation class and size
        RawAllocationSample::OnTransform(sample, valueOffset);

        sample.AddLabel(Label(Sample::ObjectAllocationTimeLabel, std::to_string(AllocationTime)));
    }

    int64_t AllocationTime;  // timestamps when the object was allocated
    intptr_t ObjectAddress;  // the address is needed until we can create a WeakHandle at the first garbage collection after the allocation
    // should we keep the WeakHandle here?    ObjectHandleID = void**
};