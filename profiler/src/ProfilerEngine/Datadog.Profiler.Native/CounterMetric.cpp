// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CounterMetric.h"

CounterMetric::CounterMetric(std::string name) :
    MetricBase(std::move(name)),
    _count{0}
{
}

void CounterMetric::Incr()
{
    _count++;
}

std::list<MetricBase::Metric> CounterMetric::GetMetrics()
{
    return std::list<Metric>{{_name + "_count", (double_t)_count.exchange(0)}};
}