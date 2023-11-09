// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "shared/src/native-src/dd_filesystem.hpp"

class FileHelper
{
public:
    static std::string GenerateFilename(std::string const& filename, std::string const& extension, std::string const& serviceName, std::string const& id = "");

private:
    static std::string GenerateFileSuffix(const std::string& applicationName, const std::string& extension, std::string const& pid, std::string const &id);

    FileHelper() = default;
};
