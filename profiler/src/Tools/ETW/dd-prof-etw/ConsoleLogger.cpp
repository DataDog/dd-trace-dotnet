// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ConsoleLogger.h"
#include <iostream>

void ConsoleLogger::Info(std::string line) const
{
    std::cout << line << std::endl;
}

void ConsoleLogger::Warn(std::string line) const
{
    std::cout << line << std::endl;
}

void ConsoleLogger::Error(std::string line) const
{
    std::cout << line << std::endl;
}
