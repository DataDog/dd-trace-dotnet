// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"

class RawContentionSample : public RawSample
{
public:
    void OnTransform(std::shared_ptr<Sample>& sample, uint32_t valueOffset) const override
    {
        uint32_t contentionCountIndex = valueOffset;
        uint32_t contentionDurationIndex = valueOffset + 1;

        sample->AddValue(1, contentionCountIndex);
        sample->AddValue(static_cast<std::int64_t>(ContentionDuration), contentionDurationIndex);

        // TODO: fake frame in case of missing callstack (to be fixed for .NET Framework missing ClrStack sibling event)
        if (Stack.size() == 0)
        {
            sample->AddFrame(EmptyModule, RootFrame);
        }
    }

    double ContentionDuration;

private:
    static constexpr inline std::string_view EmptyModule = "Application";
    static constexpr inline std::string_view RootFrame = "|lm: |ns: |ct: |fn:Lock_Contention";
};