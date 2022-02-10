// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IMetricsSender.h"

#include <memory>

class IMetricsSenderFactory
{
public:
    static std::shared_ptr<IMetricsSender> Create();

private:
    IMetricsSenderFactory() = delete;
    ~IMetricsSenderFactory() = delete;
};