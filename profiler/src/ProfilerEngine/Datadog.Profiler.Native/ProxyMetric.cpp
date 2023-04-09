// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProxyMetric.h"

ProxyMetric::ProxyMetric(std::string name, ProxyCallback callback) :
    MetricBase{std::move(name)},
    _callback{callback}
{
}

std::list<MetricBase::Metric> ProxyMetric::GetMetrics()
{
    return std::list<MetricBase::Metric>{{_name, _callback()}};
}