// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RuntimeInfo.h"
#include <sstream>

RuntimeInfo::RuntimeInfo(uint16_t dotnetMajor, uint16_t dotnetMinor, bool isFramework)
    :
#ifdef LINUX
    _os("linux"),
#else
    _os("windows"),
#endif
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

std::string RuntimeInfo::GetOs() const
{
    return _os;
}

std::string RuntimeInfo::GetClrString() const
{
    // runtime_version:
    //    framework-4.8
    //    core-6.0
    std::stringstream buffer;
    if (_isFramework)
    {
        buffer << "framework";
    }
    else
    {
        buffer << "core";
    }
    buffer << "-" << std::dec << _dotnetMajor << "." << _dotnetMinor;

    return buffer.str();
}
