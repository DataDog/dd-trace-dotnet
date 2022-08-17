// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IRuntimeInfo.h"

class RuntimeInfo : public IRuntimeInfo
{
public:
    RuntimeInfo(unsigned short dotnetMajor, unsigned short dotnetMinor, bool isFramework);

    // Inherited via IRuntimeInfo
    virtual bool IsDotnetFramework() const override;
    virtual unsigned short GetDotnetMajorVersion() const override;
    virtual unsigned short GetDotnetMinorVersion() const override;

private:
    unsigned short _dotnetMajor;
    unsigned short _dotnetMinor;
    bool _isFramework;
};
