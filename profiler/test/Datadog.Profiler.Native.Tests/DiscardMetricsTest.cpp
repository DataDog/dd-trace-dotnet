// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "DiscardMetrics.h"
#include "MetricsRegistry.h"

TEST(DiscardMetricsTest, CheckDefaultValue)
{
    auto registry = MetricsRegistry{};

    auto m = registry.GetOrRegister<DiscardMetrics>("my_discard_");
    auto metrics = m->GetMetrics();
    ASSERT_EQ(metrics.size(), 9);

    for (auto [name, value] : m->GetMetrics())
    {
        ASSERT_EQ(value, 0);
    }
}

TEST(DiscardMetricsTest, CheckOnlyOneIsIncremented)
{
    auto registry = MetricsRegistry{};

    auto m = registry.GetOrRegister<DiscardMetrics>("my_discard_");

    m->Incr<DiscardReason::ExternalSignal>();

    auto metricName = std::string("my_discard_") +  to_string(DiscardReason::ExternalSignal);
    for (auto [name, value] : m->GetMetrics())
    {
        if (metricName == name)
        {
            ASSERT_EQ(value, 1);
        }
        else
        {
            ASSERT_EQ(value, 0);
        }
    }
}

TEST(DiscardMetricsTest, CheckAllDiscardReasons)
{
    auto registry = MetricsRegistry{};

    auto m = registry.GetOrRegister<DiscardMetrics>("my_discard_");

    const std::size_t expectedMetricValue = 10;
    for (std::size_t i = 0; i < expectedMetricValue; i++)
    {
        m->Incr<DiscardReason::InSegvHandler>();
        m->Incr<DiscardReason::InsideWrappedFunction>();
        m->Incr<DiscardReason::ExternalSignal>();
        m->Incr<DiscardReason::UnknownThread>();
        m->Incr<DiscardReason::WrongManagedThread>();
        m->Incr<DiscardReason::UnsufficientSpace>();
        m->Incr<DiscardReason::EmptyBacktrace>();
        m->Incr<DiscardReason::FailedAcquiringLock>();
        m->Incr<DiscardReason::TimedOut>();
    }

    auto metrics = m->GetMetrics();
    ASSERT_EQ(9, metrics.size());

    for (auto const& [_, value] : metrics)
    {
        ASSERT_EQ(value, expectedMetricValue);
    }
}