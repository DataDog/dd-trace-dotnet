// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

class IRuntimeInfo
{
public:
    virtual ~IRuntimeInfo() = default;
    virtual bool IsDotnetFramework() const = 0;
    virtual unsigned short GetDotnetMajorVersion() const  = 0;
    virtual unsigned short GetDotnetMinorVersion() const  = 0;

// TODO: add OS details when needed
};