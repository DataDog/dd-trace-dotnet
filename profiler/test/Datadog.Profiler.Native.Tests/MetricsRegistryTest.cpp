// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MetricsRegistry.h"
#include "CounterMetric.h"
#include "MeanMaxMetric.h"
#include "ProxyMetric.h"
#include "SumMetric.h"

#include "gtest/gtest.h"

#include <chrono>
#include <functional>
#include <future>
#include <numeric>
#include <thread>

using namespace std::literals::chrono_literals;

TEST(MetricsRegistryTest, CheckAddOnMetric)
{
    auto registry = MetricsRegistry();

    auto metric = registry.GetOrRegister<MeanMaxMetric>("metric1");
    auto sumMetric = registry.GetOrRegister<SumMetric>("metric2");

    metric->Add(43);
    metric->Add(-1);

    sumMetric->Add(1024);
    sumMetric->Add(2048);

    std::unordered_map<std::string, double_t> expectedResults =
        {
            {"metric1_sum", 42},
            {"metric1_mean", 21},
            {"metric1_max", 43},
            {"metric2", 1024 + 2048}
        };

    {
        auto metrics = registry.Collect();

        ASSERT_EQ(metrics.size(), expectedResults.size());
        for (auto const& [name, value] : metrics)
        {
            ASSERT_EQ(value, expectedResults[name]);
        }
    }

    // meanmax metric are reset at each collection
    {
        auto metrics = registry.Collect();

        ASSERT_EQ(metrics.size(), expectedResults.size());
        for (auto const& [name, value] : metrics)
        {
            ASSERT_EQ(value, 0);
        }
    }
}

TEST(MetricsRegistryTest, CheckAddOnCounterMetric)
{
    auto registry = MetricsRegistry();

    auto metric = registry.GetOrRegister<CounterMetric>("metric1");

    metric->Incr();
    metric->Incr();
    metric->Incr();
    metric->Incr();

    {
        auto metrics = registry.Collect();
        ASSERT_EQ(metrics.size(), 1);
        auto const& [_, value] = metrics.front();
        ASSERT_EQ(value, 4);
    }

    // counter metric is reset at each collection
    {
        auto metrics = registry.Collect();
        ASSERT_EQ(metrics.size(), 1);
        auto const& [_, value] = metrics.front();
        ASSERT_EQ(value, 0);
    }
}

TEST(MetricsRegistryTest, CheckAddOnMultipleMetrics)
{
    auto registry = MetricsRegistry();

    auto metric = registry.GetOrRegister<MeanMaxMetric>("metric1");
    auto metric2 = registry.GetOrRegister<CounterMetric>("metric2");
    auto sumMetric = registry.GetOrRegister<SumMetric>("metric3");

    metric->Add(43);
    metric->Add(-12);
    metric->Add(5);

    metric2->Incr();
    metric2->Incr();
    metric2->Incr();
    metric2->Incr();
    metric2->Incr();
    metric2->Incr();

    sumMetric->Add(1024);
    sumMetric->Add(2048);
    sumMetric->Add(4096);

    auto metrics = registry.Collect();

    std::unordered_map<std::string, double_t> expectedResults = {
        {"metric1_sum", 36},
        {"metric1_mean", 12},
        {"metric1_max", 43},
        {"metric2_count", 6},
        {"metric3", 1024 + 2048 + 4096}};

    ASSERT_EQ(metrics.size(), expectedResults.size());

    for (auto const& [name, value] : metrics)
    {
        ASSERT_EQ(value, expectedResults[name]);
    }
}

TEST(MetricsRegistryTest, CheckSameMetricObjectForTheSameMetricName)
{
    auto registry = MetricsRegistry();

    auto metric = registry.GetOrRegister<CounterMetric>("metric1");
    auto metric2 = registry.GetOrRegister<CounterMetric>("metric1");

    ASSERT_EQ(metric, metric2);
}

TEST(MetricsRegistryTest, CheckDifferentMetricObjectForDifferentMetricName)
{
    auto registry = MetricsRegistry();

    auto metric = registry.GetOrRegister<CounterMetric>("metric1");
    auto metric2 = registry.GetOrRegister<CounterMetric>("metric2");

    ASSERT_NE(metric, metric2);
}

TEST(MetricsRegistryTest, CheckCallback)
{
    auto registry = MetricsRegistry();

    auto metric = registry.GetOrRegister<ProxyMetric>("metric1", []() { return 42; });

    {
        auto metrics = registry.Collect();

        ASSERT_EQ(metrics.size(), 1);
        auto const& [name, value] = metrics.front();
        ASSERT_EQ(name, "metric1");
        ASSERT_EQ(value, 42);
    }

    // Collect must not affect a proxy metric
    {
        auto metrics = registry.Collect();

        ASSERT_EQ(metrics.size(), 1);
        auto const& [name, value] = metrics.front();
        ASSERT_EQ(name, "metric1");
        ASSERT_EQ(value, 42);
    }
}

TEST(MetricsRegistryTest, CheckCallbackReturnsNewValueIfChanged)
{
    auto registry = MetricsRegistry();

    int v = 42;
    auto metric = registry.GetOrRegister<ProxyMetric>("metric1", [&v]() { return v; });

    {
        auto metrics = registry.Collect();

        ASSERT_EQ(metrics.size(), 1);
        auto const& [name, value] = metrics.front();
        ASSERT_EQ(name, "metric1");
        ASSERT_EQ(value, 42);
    }

    // v is updated. Make sure we get this new value
    v = 43;
    {
        auto metrics = registry.Collect();

        ASSERT_EQ(metrics.size(), 1);
        auto const& [name, value] = metrics.front();
        ASSERT_EQ(name, "metric1");
        ASSERT_EQ(value, 43);
    }
}

static bool wait_all(std::vector<std::future<void>> const& tasks, std::chrono::milliseconds maxTime)
{
    int nbRuns = 0;
    const auto sleepTime = 10ms;
    const auto maxRuns = maxTime / sleepTime;

    auto allFinished = false;
    do
    {
        allFinished = std::accumulate(tasks.cbegin(), tasks.cend(), true,
                                      [](bool result, std::future<void> const& f) {
                                          return f.wait_for(10ms) == std::future_status::ready && result;
                                      });
        std::this_thread::sleep_for(sleepTime);
    } while (!allFinished && ++nbRuns < maxRuns);

    return allFinished;
}

TEST(MetricsRegistryTest, CheckConcurrencyForCounterMetric)
{
    std::promise<void> startPromise;
    auto startFuture = startPromise.get_future();

    std::function<void(std::future<void>&, std::shared_ptr<CounterMetric>)> doWork =
        [](std::future<void>& waitFuture, std::shared_ptr<CounterMetric> metric) {
            waitFuture.wait();
            metric->Incr();
        };

    auto registry = MetricsRegistry();
    auto metric = registry.GetOrRegister<CounterMetric>("metric1");

    std::vector<std::future<void>> tasks;
    for (int i = 0; i < 20; i++)
        tasks.push_back(std::async(doWork, std::ref(startFuture), metric));

    startPromise.set_value();

    ASSERT_TRUE(wait_all(tasks, 100ms));

    auto metrics = registry.Collect();

    ASSERT_EQ(metrics.size(), 1);
    auto const& [name, value] = metrics.front();
    ASSERT_EQ(name, "metric1_count");
    ASSERT_EQ(value, 20);
}

TEST(MetricsRegistryTest, CheckConcurrencyForMeanMaxMetric)
{
    std::promise<void> startPromise;
    auto startFuture = startPromise.get_future();

    std::atomic<int16_t> counter = 0;
    std::function<void(std::future<void>&, std::shared_ptr<MeanMaxMetric>)> doWork =
        [&counter](std::future<void>& waitFuture, std::shared_ptr<MeanMaxMetric> metric) {
            waitFuture.wait();
            metric->Add(++counter);
        };

    auto registry = MetricsRegistry();
    auto metric = registry.GetOrRegister<MeanMaxMetric>("metric1");

    std::vector<std::future<void>> tasks;
    for (int i = 0; i < 20; i++)
        tasks.push_back(std::async(doWork, std::ref(startFuture), metric));

    startPromise.set_value();

    ASSERT_TRUE(wait_all(tasks, 100ms));

    auto metrics = registry.Collect();

    std::unordered_map<std::string, double_t> expectedResults =
        {
            {"metric1_sum", 210},
            {"metric1_mean", 10.5},
            {"metric1_max", 20}};

    ASSERT_EQ(metrics.size(), expectedResults.size());
    for (auto const& [name, value] : metrics)
    {
        ASSERT_EQ(value, expectedResults[name]);
    }
}

TEST(MetricsRegistryTest, CheckConcurrencyForSumMetric)
{
    std::promise<void> startPromise;
    auto startFuture = startPromise.get_future();

    std::atomic<int16_t> counter = 0;
    std::function<void(std::future<void>&, std::shared_ptr<SumMetric>)> doWork =
        [&counter](std::future<void>& waitFuture, std::shared_ptr<SumMetric> metric) {
            waitFuture.wait();
            metric->Add(++counter);
        };

    auto registry = MetricsRegistry();
    auto metric = registry.GetOrRegister<SumMetric>("metric1");

    std::vector<std::future<void>> tasks;
    for (int i = 0; i < 20; i++)
        tasks.push_back(std::async(doWork, std::ref(startFuture), metric));

    startPromise.set_value();

    ASSERT_TRUE(wait_all(tasks, 100ms));

    auto metrics = registry.Collect();

    ASSERT_EQ(metrics.size(), 1);
    auto const& [name, value] = metrics.front();
    ASSERT_EQ(name, "metric1");
    ASSERT_EQ(value, 210);
}