// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RuntimeInfo.h"

RuntimeInfo::RuntimeInfo(uint16_t dotnetMajor, uint16_t dotnetMinor, bool isFramework)
    :
    _dotnetMajor(dotnetMajor),
    _dotnetMinor(dotnetMinor),
    _isFramework(isFramework)
{
}

bool RuntimeInfo::IsDotnetFramework() const
{
    return _isFramework;
}

uint16_t RuntimeInfo::GetDotnetMajorVersion() const
{
    return _dotnetMajor;
}

uint16_t RuntimeInfo::GetDotnetMinorVersion() const
{
    return _dotnetMinor;
}
