// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <vector>

#include "Sample.h"


class TimelineSampleType
{
public:
    static const std::vector<SampleValueType> Definitions;
};