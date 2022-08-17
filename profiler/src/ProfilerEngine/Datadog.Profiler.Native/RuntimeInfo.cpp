// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RuntimeInfo.h"

RuntimeInfo::RuntimeInfo(unsigned short dotnetMajor, unsigned short dotnetMinor, bool isFramework)
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

unsigned short RuntimeInfo::GetDotnetMajorVersion() const
{
    return _dotnetMajor;
}

unsigned short RuntimeInfo::GetDotnetMinorVersion() const
{
    return _dotnetMinor;
}
