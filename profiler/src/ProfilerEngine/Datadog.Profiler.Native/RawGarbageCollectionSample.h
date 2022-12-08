// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "GarbageCollection.h"
#include "GCBaseRawSample.h"
#include "RawSample.h"
#include "Sample.h"

#include <string>
#include <vector>

class RawGarbageCollectionSample : public GCBaseRawSample
{
public:
    inline int64_t GetValue() const override
    {
        return TotalDuration;
    }

    inline void DoAdditionalTransform(std::shared_ptr<Sample> sample, uint32_t valueOffset) const override
    {
        sample->AddLabel(Label(Sample::GarbageCollectionReasonLabel, GetReasonText()));
        sample->AddLabel(Label(Sample::GarbageCollectionTypeLabel, GetTypeText()));
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

private:
    inline std::string GetReasonText() const
    {
        if (Reason >= _reasons.size())
        {
            return std::to_string(Reason);
        }

        return _reasons[Reason];
    }

    inline std::string GetTypeText() const
    {
        if (Type >= _types.size())
        {
            return std::to_string(Type);
        }

        return _types[Type];
    }

private:
// text translation of enumerations
// TODO: update it if new ones appear in forthcoming versions of .NET
    static const std::vector<std::string> _reasons;
    static const std::vector<std::string> _types;
};
