// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <list>
#include <string>
#include <vector>

#include "GroupSampler.h"
#include "SampleValueTypeProvider.h"

using UpscaleStringGroup = UpscaleGroupInfo<std::string>;

// proportional upscaling
struct UpscalingInfo
{
public:
    std::vector<SampleValueTypeProvider::Offset> const& Offsets;
    std::string LabelName;
    std::vector<UpscaleStringGroup> UpscaleGroups;
};

class IUpscaleProvider
{
public:
    virtual ~IUpscaleProvider() = default;

    virtual std::list<UpscalingInfo> GetInfos() = 0;
};

// Poisson upscaling
struct UpscalingPoissonInfo
{
public:
    std::vector<std::uintptr_t> const& Offsets;
    uint64_t SamplingDistance;
    std::uintptr_t SumOffset;
    std::uintptr_t CountOffset;
};

class IUpscalePoissonProvider
{
public:
    virtual ~IUpscalePoissonProvider() = default;

    virtual UpscalingPoissonInfo GetPoissonInfo() = 0;
};