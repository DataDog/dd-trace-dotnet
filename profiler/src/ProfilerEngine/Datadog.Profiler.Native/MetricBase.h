// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <list>
#include <math.h>
#include <string>
#include <tuple>

class MetricBase
{
public:
    using Metric = std::pair<std::string, double_t>;

    MetricBase(std::string name) :
        _name{std::move(name)}
    {
    }

    virtual ~MetricBase() = default;
    virtual std::list<Metric> GetMetrics() = 0;

protected:
    std::string _name;
};
