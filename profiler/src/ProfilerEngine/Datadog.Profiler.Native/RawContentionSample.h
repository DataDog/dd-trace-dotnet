// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"
#include "ManagedThreadInfo.h"
#include "SymbolsStore.h"

class RawContentionSample : public RawSample
{
public:
    inline static const std::string BucketLabelName = "Duration bucket";
    inline static const std::string WaitBucketLabelName = "Wait duration bucket";
    inline static const std::string RawCountLabelName = "raw count";
    inline static const std::string RawDurationLabelName = "raw duration";
    inline static const std::string BlockingThreadIdLabelName = "blocking thread id";
    inline static const std::string BlockingThreadNameLabelName = "blocking thread name";
    inline static const std::string ContentionTypeLabelName = "contention type";

public:
    RawContentionSample() = default;

    RawContentionSample(RawContentionSample&& other) noexcept
        :
        RawSample(std::move(other)),
        ContentionDuration(other.ContentionDuration),
        Bucket(std::move(other.Bucket)),
        BlockingThreadId(other.BlockingThreadId),
        BlockingThreadName(std::move(other.BlockingThreadName)),
        Type(other.Type)
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
            Type = other.Type;
        }
        return *this;
    }

    void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets, libdatadog::SymbolsStore* symbolsStore) const override
    {
        assert(valueOffsets.size() == 2);
        auto contentionCountIndex = valueOffsets[0];
        auto contentionDurationIndex = valueOffsets[1];

        // To avoid breaking the backend, always set the bucket label, but provide the wait bucket label if needed
        // This is needed to allow an upscaling different between wait and lock contentions
        sample->AddLabel(StringLabel{symbolsStore->GetBucketLabelName(), Bucket});
        if (Type == ContentionType::Wait)
        {
            sample->AddLabel(StringLabel{symbolsStore->GetWaitBucketLabelName(), std::move(Bucket)});
        }

        sample->AddValue(1, contentionCountIndex);
        sample->AddLabel(NumericLabel{symbolsStore->GetRawCountLabelName(), 1});
        sample->AddLabel(NumericLabel{symbolsStore->GetRawDurationLabelName(), ContentionDuration.count()});
        sample->AddValue(ContentionDuration.count(), contentionDurationIndex);
        if (BlockingThreadId != 0)
        {
            sample->AddLabel(NumericLabel{symbolsStore->GetBlockingThreadIdLabelName(), BlockingThreadId});
            sample->AddLabel(StringLabel{symbolsStore->GetBlockingThreadNameLabelName(), shared::ToString(BlockingThreadName)});
        }
        sample->AddLabel(StringLabel{symbolsStore->GetContentionTypeLabelName(), ContentionTypes[static_cast<int>(Type)]});
    }

    std::chrono::nanoseconds ContentionDuration;
    std::string Bucket;
    uint64_t BlockingThreadId;
    shared::WSTRING BlockingThreadName;
    ContentionType Type;

    static std::string ContentionTypes[static_cast<int>(ContentionType::ContentionTypeCount)];
};

inline std::string RawContentionSample::ContentionTypes[static_cast<int>(ContentionType::ContentionTypeCount)] =
{
    "Unknown",
    "Lock",
    "Wait",
};