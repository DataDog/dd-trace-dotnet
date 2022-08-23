// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RuntimeInfo.h"
#include "RuntimeInfoHelper.h"


RuntimeInfoHelper::RuntimeInfoHelper(uint16_t major, uint16_t minor, bool isFramework)
{
    _runtimeInfo = std::make_unique<RuntimeInfo>(major, minor, isFramework);
}

IRuntimeInfo* RuntimeInfoHelper::GetRuntimeInfo() const
{
    return _runtimeInfo.get();
}
