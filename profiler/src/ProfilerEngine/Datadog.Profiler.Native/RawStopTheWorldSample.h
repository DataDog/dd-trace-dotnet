// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawStopTheWorldSample : public RawSample
{
public:
    inline void OnTransform(Sample& sample, uint32_t valueOffset) const override
    {
        uint32_t durationIndex = valueOffset;

        sample.AddValue(Duration, durationIndex);

        sample.AddLabel(Label(Sample::GarbageCollectionGenerationLabel, std::to_string(Generation)));
        sample.AddLabel(Label(Sample::GarbageCollectionNumberLabel, std::to_string(Number)));

        BuildCallStack(sample, Generation);
    }

public:
     int32_t Number;
     uint32_t Generation;
     int64_t Duration;

private:
    // each Stop the World garbage collection will share the same root frame and the second one will show the collected generation
    const std::string EmptyModule = "CLR";
    const std::string RootFrame = "|lm: |ns: |ct: |fn:Garbage Collector";
    const std::string Gen0Frame = "|lm: |ns: |ct: |fn:gen0";
    const std::string Gen1Frame = "|lm: |ns: |ct: |fn:gen1";
    const std::string Gen2Frame = "|lm: |ns: |ct: |fn:gen2";
    const std::string UnknownGenerationFrame = "|lm: |ns: |ct: |fn:unknown";

    void BuildCallStack(Sample& sample, uint32_t generation) const
    {
        // add same root frame
        sample.AddFrame(EmptyModule, RootFrame);

        // add generation based frame
        switch (generation)
        {
            case 0:
                sample.AddFrame(EmptyModule, Gen0Frame);
                break;

            case 1:
                sample.AddFrame(EmptyModule, Gen1Frame);
                break;

            case 2:
                sample.AddFrame(EmptyModule, Gen2Frame);
                break;

            default:
                sample.AddFrame(EmptyModule, UnknownGenerationFrame);
                break;
        }
    }
};