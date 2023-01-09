// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SumMetric.h"

#include <utility>

SumMetric::SumMetric(std::string name) :
    MetricBase(std::move(name)),
    _value{0}
{
}

void SumMetric::Add(double_t v)
{
    std::unique_lock lock(_lock);
    _value += v;
}

std::list<MetricBase::Metric> SumMetric::GetMetrics()
{
    double_t value = 0;

    {
        std::unique_lock lock(_lock);

        value = std::exchange(_value, value);
    }

    return std::list<Metric>{{_name, value}};
}