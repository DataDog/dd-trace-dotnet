// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FileHelper.h"

#include "OpSysTools.h"

#include <fstream>

std::string FileHelper::GenerateFilename(std::string const& filename, std::string const& extension, std::string const& serviceName, std::string const& id)
{
    static std::string pid = std::to_string(OpSysTools::GetProcId());

    auto fileSuffix = GenerateFileSuffix(serviceName, extension, pid, id);

    if (filename.empty())
    {
        return fileSuffix;
    }
    return filename + "_" + fileSuffix;
}

std::string FileHelper::GenerateFileSuffix(const std::string& applicationName, const std::string& extension, std::string const& pid, std::string const& id)
{
    std::stringstream oss;
    oss << applicationName + "_" << pid << "_";

    if (id.empty())
    {
        auto time = std::time(nullptr);
        struct tm buf = {};

#ifdef _WINDOWS
        localtime_s(&buf, &time);
#else
        localtime_r(&time, &buf);
#endif
        oss << std::put_time(&buf, "%F_%H-%M-%S");
    }
    else
    {
        oss << id;
    }

    oss << extension;
    return oss.str();
}
