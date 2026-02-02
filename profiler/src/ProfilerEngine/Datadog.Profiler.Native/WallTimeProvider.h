// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "RawSampleTransformer.h"
#include "RawWallTimeSample.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

// forward declarations
class IConfiguration;
class IThreadsCpuManager;
namespace libdatadog {
class SymbolsStore;
}

class WallTimeProvider
    :
    public CollectorBase<RawWallTimeSample> // accepts raw walltime samples
{
public:
    WallTimeProvider(
        SampleValueTypeProvider& sampleTypeProvider,
        RawSampleTransformer* rawSampleTransformer,
        shared::pmr::memory_resource* memoryResource,
        libdatadog::SymbolsStore* pSymbolsStore
        );

private:
    static std::vector<SampleValueType> SampleTypeDefinitions;
};
