// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <iostream>
#include <string>

#include "IMetricsSender.h"

class DogstatsdService : public IMetricsSender
{
public:
    DogstatsdService(const std::string& host, int port, const Tags& tags);
    ~DogstatsdService() override = default;

    bool Gauge(const std::string& name, double value) override;
    bool Counter(const std::string& name, std::uint64_t value) override;

public:
    enum class MetricType;

private:
    template <DogstatsdService::MetricType metricType, typename ValueType>
    bool Send(const std::string& name, ValueType value);

private:
    const Tags _commonTags;
    const std::string _host;
    int _port;
};