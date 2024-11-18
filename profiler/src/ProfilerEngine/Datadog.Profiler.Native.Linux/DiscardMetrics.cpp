// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DiscardMetrics.h"

DiscardMetrics::DiscardMetrics(std::string name) :
    MetricBase(std::move(name)), _metrics{0}
{
}

std::list<MetricBase::Metric> DiscardMetrics::GetMetrics()
{
    std::list<MetricBase::Metric> result;
    for (std::size_t idx = 0; idx < _metrics.size(); idx++)
    {
        auto& metric = _metrics[idx];
        result.emplace_back(
            std::make_pair(_name + to_string(static_cast<DiscardReason>(idx)),
                           metric.exchange(0)));
    }
    return result;
}