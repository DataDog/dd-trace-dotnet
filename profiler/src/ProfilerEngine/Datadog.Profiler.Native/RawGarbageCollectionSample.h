// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "GarbageCollection.h"
#include "GCBaseRawSample.h"
#include "RawSample.h"
#include "Sample.h"

class RawGarbageCollectionSample : public GCBaseRawSample
{
public:
    inline int64_t GetValue() const override
    {
        return TotalDuration;
    }

    inline void DoAdditionalTransform(std::shared_ptr<Sample> sample, uint32_t valueOffset) const override
    {
        sample->AddLabel(Label(Sample::GarbageCollectionReasonLabel, std::to_string(Reason)));
        sample->AddLabel(Label(Sample::GarbageCollectionTypeLabel, std::to_string(Type)));
        sample->AddLabel(Label(Sample::GarbageCollectionCompactingLabel, (IsCompacting ? "true" : "false")));

        // set event type
        sample->AddLabel(Label(Sample::TimelineEventTypeLabel, Sample::TimelineEventTypeGarbageCollection));
    }

public:
    GCReason Reason;
    GCType Type;
    bool IsCompacting;
    uint64_t PauseDuration;  // not used today
    uint64_t TotalDuration;
};
