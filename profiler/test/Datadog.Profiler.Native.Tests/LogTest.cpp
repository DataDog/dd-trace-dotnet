// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "Log.h"
#include "EnvironmentVariables.h"

#include <fstream>

#include "EnvironmentHelper.h"

#include "shared/src/native-src/dd_filesystem.hpp"
#include "shared/src/native-src/pal.h"

extern void unsetenv(const shared::WSTRING& name);

void CheckExpectedStringInFile(fs::path const& fileFullPath, std::string const& expectedString)
{
    std::string line;
    std::ifstream ifs(fileFullPath.native());
    while (std::getline(ifs, line))
    {
        auto it = line.find(expectedString);
        if (it != std::string::npos)
        {
            return;
        }
    }

    ASSERT_FALSE(true);
}

TEST(LoggerTest, EnsureLogFilesAreFoundAtDefaultLocation)
{
    unsetenv(EnvironmentVariables::LogDirectory); // to make sure this env. var. is not set (other test)

    std::string expectedString = "This is a test <EnsureByDefaultLogFilesAreInProgramData>";
    Log::Error(expectedString);

    std::string applicationNameNoExtension = fs::path(::shared::ToString(::shared::GetCurrentProcessName())).replace_extension().string();
    std::string expectedLogFilename = "DD-DotNet-Profiler-Native-" + applicationNameNoExtension + "-" + std::to_string(::shared::GetPID()) + ".log";

    fs::path expectedLogFileFullPath =
#ifdef _WINDOWS
        "C:\\ProgramData\\Datadog .NET Tracer\\logs\\" + expectedLogFilename;
#else
        "/var/log/datadog/dotnet/" + expectedLogFilename;
#endif

    ASSERT_TRUE(fs::exists(expectedLogFileFullPath));

    CheckExpectedStringInFile(expectedLogFileFullPath, expectedString);
}
