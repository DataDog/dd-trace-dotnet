// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <string>
#include <utility>
#include <vector>
#include <cstdint>

class IMetricsSender
{
public:
    using Tag = std::pair<std::string, std::string>;
    using Tags = std::vector<Tag>;

    IMetricsSender() = default;
    virtual ~IMetricsSender() = default;

    virtual bool Gauge(const std::string& name, double value) = 0;
    virtual bool Counter(const std::string& name, std::uint64_t value, const Tags& additionalTags = {}) = 0;
};
