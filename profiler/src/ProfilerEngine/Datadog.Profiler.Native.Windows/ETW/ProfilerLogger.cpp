// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfilerLogger.h"
#include "Log.h"

void ProfilerLogger::Info(std::string line) const
{
    Log::Info(std::move(line));
}

void ProfilerLogger::Warn(std::string line) const
{
    Log::Warn(std::move(line));
}

void ProfilerLogger::Error(std::string line) const
{
    Log::Error(std::move(line));
}
