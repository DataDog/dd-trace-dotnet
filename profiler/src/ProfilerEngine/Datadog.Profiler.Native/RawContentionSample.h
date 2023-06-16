// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawContentionSample : public RawSample
{
public:
    inline static const std::string BucketLabelName = "Duration bucket";
    inline static const std::string RawCountLabelName = "raw count";
    inline static const std::string RawDurationLabelName = "raw duration";

public:
    RawContentionSample() = default;

    RawContentionSample(RawContentionSample&& other) noexcept
        :
        RawSample(std::move(other)),
        ContentionDuration(other.ContentionDuration),
        Bucket(std::move(other.Bucket))
    {
    }

    RawContentionSample& operator=(RawContentionSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            ContentionDuration = other.ContentionDuration;
            Bucket = std::move(other.Bucket);
        }
        return *this;
    }

    void OnTransform(std::shared_ptr<Sample>& sample, uint32_t valueOffset) const override
    {
        uint32_t contentionCountIndex = valueOffset;
        uint32_t contentionDurationIndex = valueOffset + 1;

        sample->AddLabel(Label{BucketLabelName, std::move(Bucket)});
        sample->AddValue(1, contentionCountIndex);
        sample->AddLabel(Label{RawCountLabelName, std::to_string(1)});
        sample->AddLabel(Label{RawDurationLabelName, std::to_string(static_cast<uint64_t>(ContentionDuration))});
        sample->AddValue(static_cast<std::int64_t>(ContentionDuration), contentionDurationIndex);
    }

    double ContentionDuration;
    std::string Bucket;
};