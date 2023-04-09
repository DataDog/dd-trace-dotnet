// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "MetricBase.h"

#include <limits>
#include <list>
#include <mutex>
#include <string>

class MeanMaxMetric : public MetricBase
{
public:
    MeanMaxMetric(std::string name);

    void Add(double_t v);

    std::list<Metric> GetMetrics() override;

private:
    double_t _value;
    uint64_t _total;
    double_t _max;
    std::mutex _lock;
};