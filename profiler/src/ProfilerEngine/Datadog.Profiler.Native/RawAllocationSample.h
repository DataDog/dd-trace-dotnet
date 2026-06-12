// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawAllocationSample : public RawSample
{
public:
    RawAllocationSample() = default;

    RawAllocationSample(RawAllocationSample&& other) noexcept
        :
        RawSample(std::move(other)),
        AllocationClass(std::move(other.AllocationClass)),
        AllocationSize(other.AllocationSize),
        Address(other.Address),
        MethodTable(other.MethodTable)
    {
    }

    RawAllocationSample& operator=(RawAllocationSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            AllocationClass = std::move(other.AllocationClass);
            AllocationSize = other.AllocationSize;
            Address = other.Address;
            MethodTable = other.MethodTable;
        }
        return *this;
    }

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets, libdatadog::SymbolsStore* symbolsStore) const override
    {
        auto allocationCountIndex = valueOffsets[0];
        sample->AddValue(1, allocationCountIndex);

        // in .NET Framework, no size is available
        if (valueOffsets.size() == 2)
        {
            auto allocationSizeIndex = valueOffsets[1];
            sample->AddValue(AllocationSize, allocationSizeIndex);
        }

        sample->AddLabel(StringLabel(symbolsStore->GetAllocationClass(), AllocationClass));
    }

    std::string AllocationClass;
    int64_t AllocationSize;
    uintptr_t Address;
    ClassID MethodTable;
};