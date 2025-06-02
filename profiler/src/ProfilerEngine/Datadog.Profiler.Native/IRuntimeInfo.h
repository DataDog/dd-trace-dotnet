// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>

class IRuntimeInfo
{
public:
    virtual ~IRuntimeInfo() = default;
    virtual bool IsDotnetFramework() const = 0;
    virtual uint16_t GetMajorVersion() const  = 0;
    virtual uint16_t GetMinorVersion() const = 0;
    virtual std::string GetOs() const = 0;
    virtual std::string GetClrString() const = 0;

    // for .NET Framework, we get the exact minor version
    // after mscorlib gets loaded
    virtual void SetMinorVersions(uint16_t minor, uint16_t build, uint16_t reviews) = 0;

    // TODO: add OS details when needed
};