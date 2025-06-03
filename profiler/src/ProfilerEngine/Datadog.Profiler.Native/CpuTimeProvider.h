// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "RawCpuSample.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

// forward declarations
class IConfiguration;
class SampleValueTypeProvider;
class RawSampleTransformer;

class CpuTimeProvider
    :
    public CollectorBase<RawCpuSample> // accepts cputime samples
{
public:
    CpuTimeProvider(
        SampleValueTypeProvider& valueTypeProvider,
        RawSampleTransformer* rawSampleTransformer,
        shared::pmr::memory_resource* memoryResource);

    static std::vector<SampleValueType> SampleTypeDefinitions;
};
