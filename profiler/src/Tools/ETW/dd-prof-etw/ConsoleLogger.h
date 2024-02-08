// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\IIpcLogger.h"

class ConsoleLogger : public IIpcLogger
{
    // Inherited via IIpcLogger
    void Info(std::string line) const override;
    void Warn(std::string line) const override;
    void Error(std::string line) const override;
};
