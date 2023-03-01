// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>
#include <vector>

struct UpscaleGroupInfo
{
public:
    std::string Name;
    uint64_t RealCount;
    uint64_t SampledCount;
};

class IUpscaleProvider
{
public:
    virtual ~IUpscaleProvider() = default;

    virtual bool GetGroups(std::vector<UpscaleGroupInfo>& groups) = 0;
};