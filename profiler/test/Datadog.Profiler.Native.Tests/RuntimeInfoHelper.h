// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <memory>

#include "IRuntimeInfo.h"

class RuntimeInfoHelper
{
public:
    RuntimeInfoHelper(uint16_t major, uint16_t minor, bool isFramework);

    IRuntimeInfo* GetRuntimeInfo() const;

 private:
    std::unique_ptr<IRuntimeInfo> _runtimeInfo;
};
