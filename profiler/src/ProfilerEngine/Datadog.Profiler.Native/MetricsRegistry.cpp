// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MetricsRegistry.h"

#include <cassert>


MetricsRegistry::Metrics MetricsRegistry::Collect()
{
    auto metrics = Metrics();

    {
        std::unique_lock<std::mutex> lock(_registryLock);
        for (auto const& [_, bucket] : _metrics)
        {
            metrics.splice(metrics.end(), bucket->GetMetrics());
        }
    }

    return metrics;
}
