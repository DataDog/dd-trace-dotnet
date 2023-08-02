// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "GCBaseRawSample.h"
#include "GarbageCollection.h"
#include "RawSample.h"
#include "Sample.h"

#include <string>
#include <vector>

class RawGarbageCollectionSample : public GCBaseRawSample
{
public:
    RawGarbageCollectionSample() = default;

    RawGarbageCollectionSample(RawGarbageCollectionSample&& other) noexcept
        :
        GCBaseRawSample(std::move(other)),
        Reason(other.Reason),
        Type(other.Type),
        IsCompacting(other.IsCompacting),
        PauseDuration(other.PauseDuration),
        TotalDuration(other.TotalDuration)
    {
    }

    RawGarbageCollectionSample& operator=(RawGarbageCollectionSample&& other) noexcept
    {
        if (this == &other)
        {
            GCBaseRawSample::operator=(std::move(other));
            Reason = other.Reason;
            Type = other.Type;
            IsCompacting = other.IsCompacting;
            PauseDuration = other.PauseDuration;
            TotalDuration = other.TotalDuration;
        }
        return *this;
    }

    inline int64_t GetValue() const override
    {
        return TotalDuration;
    }

    inline void DoAdditionalTransform(std::shared_ptr<Sample> sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
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
    uint64_t PauseDuration; // not used today
    uint64_t TotalDuration;

private:
    inline std::string GetReasonText() const
    {
        if ((size_t)Reason >= _reasons.size())
        {
            return std::to_string(Reason);
        }

        return _reasons[Reason];
    }

    inline std::string GetTypeText() const
    {
        if ((size_t)Type >= _types.size())
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
