// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <iostream>
#include <string>
#include <cstdint>

#include "IMetricsSender.h"

class DogstatsdService : public IMetricsSender
{
public:
    DogstatsdService(const std::string& host, int32_t port, const Tags& tags);
    ~DogstatsdService() override = default;

    bool Gauge(const std::string& name, double value) override;
    bool Counter(const std::string& name, std::uint64_t value, const Tags& additionalTasg = {}) override;

public:
    enum class MetricType;

private:
    static Tags MergeTags(const Tags& tags_, const Tags& other);

    template <DogstatsdService::MetricType metricType, typename ValueType>
    bool Send(const std::string& name, ValueType value, const Tags& additionalTasg = {});

private:
    const Tags _commonTags;
    const std::string _host;
    int32_t _port;
};