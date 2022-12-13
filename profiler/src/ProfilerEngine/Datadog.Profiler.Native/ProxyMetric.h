// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "MetricBase.h"

#include <functional>
#include <list>
#include <string>

class ProxyMetric : public MetricBase
{
public:
    using ProxyCallback = std::function<double_t()>;

    ProxyMetric(std::string name, ProxyCallback callback);

    std::list<Metric> GetMetrics() override;

private:
    ProxyCallback _callback;
};