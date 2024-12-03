// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <array>
#include <atomic>
#include <list>
#include <string>
#include <utility>

#include "MetricBase.h"
#include "DiscardReason.h"

class DiscardMetrics : public MetricBase
{
private:
    static constexpr std::size_t array_size = static_cast<std::size_t>(DiscardReason::GuardItem);

public:
    explicit DiscardMetrics(std::string name);

    template <DiscardReason TType>
    void Incr()
    {
        static_assert(TType != DiscardReason::GuardItem, "You must not use DiscardReason::GuardItem");
        constexpr auto offset = static_cast<int>(TType);
        static_assert(offset <= array_size, "Unknown TType");
        static_assert(0 <= offset, "Unknown TType (offset is negative)");
        _metrics[offset]++;
    }

    std::list<MetricBase::Metric> GetMetrics() override;

private:
    std::array<std::atomic<std::uint64_t>, array_size> _metrics;
};
