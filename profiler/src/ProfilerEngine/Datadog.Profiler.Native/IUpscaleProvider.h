// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "GroupSampler.h"
#include "SampleValueTypeProvider.h"

using UpscaleStringGroup = UpscaleGroupInfo<std::string>;

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

    virtual UpscalingInfo GetInfo() = 0;
};