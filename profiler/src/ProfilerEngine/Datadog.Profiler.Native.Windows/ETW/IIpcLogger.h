// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

class IIpcLogger
{
public:
    virtual ~IIpcLogger() = default;
    virtual void Info(std::string line) const = 0;
    virtual void Warn(std::string line) const = 0;
    virtual void Error(std::string line) const = 0;
};