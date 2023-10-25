// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IRuntimeInfo.h"

class RuntimeInfo : public IRuntimeInfo
{
public:
    RuntimeInfo(uint16_t dotnetMajor, uint16_t dotnetMinor, bool isFramework);

    // Inherited via IRuntimeInfo
    bool IsDotnetFramework() const override;
    uint16_t GetDotnetMajorVersion() const override;
    uint16_t GetDotnetMinorVersion() const override;
    std::string GetOs() const override;
    std::string GetClrString() const override;

private:
    uint16_t _dotnetMajor;
    uint16_t _dotnetMinor;
    bool _isFramework;
    std::string _os;
};
