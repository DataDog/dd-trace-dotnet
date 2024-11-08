// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawNetworkSample : public RawSample
{
public:
    RawNetworkSample() = default;

    RawNetworkSample(RawNetworkSample&& other) noexcept
        :
        RawSample(std::move(other)),
        Url(std::move(other.Url))
    {
    }

    RawNetworkSample& operator=(RawNetworkSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            Url = std::move(other.Url);
        }
        return *this;
    }

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
    {
        auto networkCountIndex = valueOffsets[0];
        sample->AddValue(EndTimestamp - StartTimestamp, networkCountIndex);

        sample->AddLabel(Label(Sample::RequestUrlLabel, Url));
        sample->AddNumericLabel(NumericLabel(Sample::RequestTimeStampLabel, StartTimestamp));

    }

    std::string Url;
    uint64_t StartTimestamp;
    uint64_t EndTimestamp;
    int32_t StatusCode;
    std::string Error;
    std::string EndThreadId;
    // TODO: check with BE if we also need the thread name
};