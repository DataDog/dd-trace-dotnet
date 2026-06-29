// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"
#include "SymbolsStore.h"

class GCBaseRawSample : public RawSample
{
public:
    GCBaseRawSample() = default;

    GCBaseRawSample(GCBaseRawSample&& other) noexcept
        :
        RawSample(std::move(other)),
        Number(other.Number),
        Generation(other.Generation),
        Duration(other.Duration)
    {
    }

    GCBaseRawSample& operator=(GCBaseRawSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            Number = other.Number;
            Generation = other.Generation;
            Duration = other.Duration;
        }
        return *this;
    }

    // This base class is in charge of storing garbage collection number and generation as labels
    // and fill up the callstack based on generation.
    // The default value is the Duration field; derived class could override by implementing GetValue()
    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets, libdatadog::SymbolsStore* symbolsStore) const override
    {
        assert(valueOffsets.size() == 1);
        auto durationIndex = valueOffsets[0];

        sample->AddValue(GetValue(), durationIndex);

        sample->AddLabel(NumericLabel(symbolsStore->GetGarbageCollectionNumber(), Number));
        AddGenerationLabel(sample, Generation, symbolsStore);

        BuildCallStack(sample, Generation, symbolsStore);

        // let child classes transform additional fields if needed
        DoAdditionalTransform(sample, valueOffsets, symbolsStore);
    }

    // Each derived class provides the duration to store as the value for this sample
    // By default, use the Duration field
    virtual int64_t GetValue() const
    {
        return Duration.count();
    }

    // Derived classes are expected to set the event type + any additional field as label
    virtual void DoAdditionalTransform(std::shared_ptr<Sample> sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffset, libdatadog::SymbolsStore* symbolsStore) const = 0;

public:
    int32_t Number;
    uint32_t Generation;
    std::chrono::nanoseconds Duration;

private:
    inline static void AddGenerationLabel(std::shared_ptr<Sample>& sample, uint32_t generation, libdatadog::SymbolsStore* symbolsStore)
    {
        // we currently don't store the generation as a numeric label because there is no way to
        // make the difference between a 0 value and a 0 string index (i.e. empty string)
        auto gcGenStrigId = symbolsStore->GetGarbageCollectionGeneration();
        switch (generation)
        {
            case 0:
                sample->AddLabel(StringLabel(gcGenStrigId, Gen0Value));
                break;

            case 1:
                sample->AddLabel(StringLabel(gcGenStrigId, Gen1Value));
                break;

            case 2:
                    sample->AddLabel(StringLabel(gcGenStrigId, Gen2Value));
                break;

            default: // this should never happen (only gen0, gen1 or gen2 collections)
                sample->AddLabel(StringLabel(gcGenStrigId, std::to_string(generation)));
                break;
        }
    }

    inline static void BuildCallStack(std::shared_ptr<Sample>& sample, uint32_t generation, libdatadog::SymbolsStore* symbolsStore)
    {
        // add same root frame

        auto clrModuleId = symbolsStore->GetClrModuleId();

        sample->AddFrame({clrModuleId, symbolsStore->GetGCRootFrameId(), 0});

        // add generation based frame
        switch (generation)
        {
            case 0:
                sample->AddFrame({clrModuleId, symbolsStore->GetGen0FrameId(), 0});
                break;

            case 1:
                sample->AddFrame({clrModuleId, symbolsStore->GetGen1FrameId(),0});
                break;

            case 2:
                sample->AddFrame({clrModuleId, symbolsStore->GetGen2FrameId(), 0});
                break;

            default:
                sample->AddFrame({clrModuleId, symbolsStore->GetUnknownGenFrameId(), 0});
                break;
        }
    }

private:
    static const inline std::string Gen0Value = "0";
    static const inline std::string Gen1Value = "1";
    static const inline std::string Gen2Value = "2";
};