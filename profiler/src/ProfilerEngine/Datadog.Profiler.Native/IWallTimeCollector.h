// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IService.h"

// forward declarations
class WallTimeSampleRaw;


class IWallTimeCollector : public IService
{
public:
    virtual void Add(WallTimeSampleRaw&& sample) = 0;
};