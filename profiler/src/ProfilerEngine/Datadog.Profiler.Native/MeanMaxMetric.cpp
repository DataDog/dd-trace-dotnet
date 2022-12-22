// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MeanMaxMetric.h"

#include <utility>

MeanMaxMetric::MeanMaxMetric(std::string name) :
    MetricBase(std::move(name)),
    _value{0},
    _total{0},
    _max{std::numeric_limits<double_t>::min()}
{
}

void MeanMaxMetric::Add(double_t v)
{
    std::unique_lock lock(_lock);
    _total++;
    _value += v;
    _max = std::max(v, _max);
}

std::list<MetricBase::Metric> MeanMaxMetric::GetMetrics()
{
    double_t value = 0;
    uint64_t total = 0;
    double_t max = std::numeric_limits<double_t>::min();

    {
        std::unique_lock lock(_lock);

        value = std::exchange(_value, value);
        total = std::exchange(_total, total);
        max = std::exchange(_max, max);
    }
    auto mean = total == 0 ? 0 : (double_t)value / total;
    max = std::numeric_limits<double_t>::min() == max ? 0 : max;

    return std::list<Metric>{
        {_name + "_sum", value},
        {_name + "_mean", mean},
        {_name + "_max", max}};
}