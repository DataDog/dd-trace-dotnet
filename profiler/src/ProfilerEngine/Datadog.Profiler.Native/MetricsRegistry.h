// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "MetricBase.h"

#include <algorithm>
#include <atomic>
#include <functional>
#include <limits>
#include <list>
#include <memory>
#include <mutex>
#include <string>
#include <tuple>
#include <unordered_map>

class MetricsRegistry
{
public:
    using Metrics = std::list<MetricBase::Metric>;

    MetricsRegistry() noexcept = default;

    MetricsRegistry(MetricsRegistry&&) noexcept = delete;
    MetricsRegistry& operator=(MetricsRegistry&&) noexcept = delete;
    MetricsRegistry(MetricsRegistry const&) = delete;
    MetricsRegistry& operator=(MetricsRegistry const&) = delete;

    template <class T, class... Types>
    std::shared_ptr<T> GetOrRegister(std::string name, Types&&... args)
    {
        std::unique_lock<std::mutex> lock(_registryLock);
        auto& metric = _metrics[name];
        if (metric != nullptr)
        {
            return std::dynamic_pointer_cast<T>(metric);
        }

        auto newMetric = std::make_shared<T>(std::move(name), std::forward<Types>(args)...);
        metric = newMetric;
        return newMetric;
    }

    Metrics Collect();

private:
    std::mutex _registryLock;
    std::unordered_map<std::string, std::shared_ptr<MetricBase>> _metrics;
};
