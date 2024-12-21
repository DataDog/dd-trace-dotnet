// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"
#include "ManagedThreadInfo.h"

class RawContentionSample : public RawSample
{
public:
    inline static const std::string BucketLabelName = "Duration bucket";
    inline static const std::string RawCountLabelName = "raw count";
    inline static const std::string RawDurationLabelName = "raw duration";
    inline static const std::string BlockingThreadIdLabelName = "blocking thread id";
    inline static const std::string BlockingThreadNameLabelName = "blocking thread name";
    inline static const std::string WaitTypeLabelName = "wait type";

public:
    RawContentionSample() = default;

    RawContentionSample(RawContentionSample&& other) noexcept
        :
        RawSample(std::move(other)),
        ContentionDuration(other.ContentionDuration),
        Bucket(std::move(other.Bucket)),
        BlockingThreadId(other.BlockingThreadId),
        BlockingThreadName(std::move(other.BlockingThreadName)),
        WaitType(other.WaitType)
    {
    }

    RawContentionSample& operator=(RawContentionSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            ContentionDuration = other.ContentionDuration;
            Bucket = std::move(other.Bucket);
            BlockingThreadId = other.BlockingThreadId;
            BlockingThreadName = std::move(other.BlockingThreadName);
            WaitType = other.WaitType;
        }
        return *this;
    }

    void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
    {
        assert(valueOffsets.size() == 2);
        auto contentionCountIndex = valueOffsets[0];
        auto contentionDurationIndex = valueOffsets[1];

        sample->AddLabel(Label{BucketLabelName, std::move(Bucket)});
        sample->AddValue(1, contentionCountIndex);
        sample->AddNumericLabel(NumericLabel{RawCountLabelName, 1});
        sample->AddNumericLabel(NumericLabel{RawDurationLabelName, ContentionDuration.count()});
        sample->AddValue(ContentionDuration.count(), contentionDurationIndex);
        if (BlockingThreadId != 0)
        {
            sample->AddNumericLabel(NumericLabel{BlockingThreadIdLabelName, BlockingThreadId});
            sample->AddLabel(Label{BlockingThreadNameLabelName, shared::ToString(BlockingThreadName)});
        }
        sample->AddLabel(Label{WaitTypeLabelName, WaitTypes[static_cast<int>(WaitType)]});
    }

    std::chrono::nanoseconds ContentionDuration;
    std::string Bucket;
    uint64_t BlockingThreadId;
    shared::WSTRING BlockingThreadName;
    WaitType WaitType;

    static std::string WaitTypes[static_cast<int>(WaitType::LastWait)];
};

inline std::string RawContentionSample::WaitTypes[static_cast<int>(WaitType::LastWait)] =
{
    "Unknown",
    "Lock",
    "Mutex",
    "Semaphore",
    "AutoResetEvent",
    "ManualResetEvent"
};