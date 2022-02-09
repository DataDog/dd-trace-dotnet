// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// this header needs to be the first one
// Otherwise we get compilation error due to header files order issue (winsock2.h and other windows header files)
#include "DogFood.hpp"

#include "DogstatsdService.h"

/// How to add a new type of Metric ?
/// - Update the IMetricsSender interface
///     add a new `MyMetric(const std::string& name, std::uint64_t value, const Tags& tags) = 0` pure virtual method
/// - Modify DogstatsdService .h and .cpp files:
///     declare `MyMetric(const std::string& name, std::uint64_t value, const Tags& tags) override`
///     add the new metric (MyMetric in this example) in the enum `MetricType`
///     implements `MyMetric(const std::string& name, std::uint64_t value, const Tags& tags)` (you can take Counter as an example)
/// - Add a conversion to DogFood::MetricType in the Convert function :
///     Add a new `if constexpr(...)` statement in Convert() for the new metric type

DogstatsdService::DogstatsdService(const std::string& host, int port, const Tags& tags) :
    _commonTags{tags},
    _host{host},
    _port{port}
{
}

enum class DogstatsdService::MetricType
{
    Gauge,
    Counter
};

bool DogstatsdService::Gauge(const std::string& name, double value)
{
    return Send<DogstatsdService::MetricType::Gauge>(name, value);
}

bool DogstatsdService::Counter(const std::string& name, std::uint64_t value)
{
    return Send<DogstatsdService::MetricType::Counter>(name, value);
}

/// <summary>
/// /!\ This function must resides in the CPP file to avoid exposing DogFood::* types (compilation and linking errors)
/// This constexpr function converts our metric type to DogFood metric type at compile-time.

/// If no conversion is possible, there is a compilation error
/// </summary>
template <DogstatsdService::MetricType metricType>
constexpr DogFood::MetricType Convert()
{
    if constexpr (metricType == DogstatsdService::MetricType::Counter)
        return DogFood::MetricType::Counter;

    if constexpr (metricType == DogstatsdService::MetricType::Gauge)
        return DogFood::MetricType::Gauge;

    return static_cast<DogFood::MetricType>(-1);
}

template <DogstatsdService::MetricType metric, typename ValueType>
bool DogstatsdService::Send(const std::string& name, ValueType value)
{
    constexpr DogFood::MetricType dogFoodMetric = Convert<metric>();
    static_assert(dogFoodMetric != -1, "No metric type conversion found.");

    return DogFood::Send(DogFood::Metric(name, value, dogFoodMetric, 1.0, _commonTags), DogFood::Configuration(DogFood::Mode::UDP, _host, _port));
}