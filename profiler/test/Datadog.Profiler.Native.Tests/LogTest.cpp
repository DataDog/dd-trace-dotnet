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

bool FindStringInLogFile(fs::path const& logFile, std::string const& expectedString)
{
    std::string line;
    std::ifstream ifs(logFile.native());
    while (std::getline(ifs, line))
    {
        auto it = line.find(expectedString);
        if (it != std::string::npos)
        {
            return true;
        }
    }

    return false;
}

static fs::path GetCurrentFileLogPath()
{
    std::string applicationNameNoExtension = fs::path(::shared::ToString(::shared::GetCurrentProcessName())).replace_extension().string();
    std::string expectedLogFilename = "DD-DotNet-Profiler-Native-" + applicationNameNoExtension + "-" + std::to_string(::shared::GetPID()) + ".log";

    fs::path expectedLogFileFullPath =
#ifdef _WINDOWS
        "C:\\ProgramData\\Datadog .NET Tracer\\logs\\" + expectedLogFilename;
#else
        "/var/log/datadog/dotnet/" + expectedLogFilename;
#endif
    return expectedLogFileFullPath;
}

TEST(LoggerTest, EnsureLogFilesAreFoundAtDefaultLocation)
{
    unsetenv(EnvironmentVariables::LogDirectory); // to make sure this env. var. is not set (other test)

    std::string expectedString = "This is a test <EnsureByDefaultLogFilesAreInProgramData>";
    Log::Error(expectedString);

    auto expectedLogFileFullPath = GetCurrentFileLogPath();

    ASSERT_TRUE(fs::exists(expectedLogFileFullPath));

    ASSERT_TRUE(FindStringInLogFile(expectedLogFileFullPath, expectedString));
}

TEST(LoggerTest, FixBugStaticWcharArray)
{
    unsetenv(EnvironmentVariables::LogDirectory); // to make sure this env. var. is not set (other test)

    auto logFilePath = GetCurrentFileLogPath();

    Log::Info(WStr("<Only Me>"));

    ASSERT_TRUE(fs::exists(logFilePath));
    ASSERT_TRUE(FindStringInLogFile(logFilePath, "<Only Me>"));

    WCHAR other[20] = WStr("Visible\0Garbage");
    Log::Warn(other);

    ASSERT_TRUE(FindStringInLogFile(logFilePath, "Visible"));
    ASSERT_FALSE(FindStringInLogFile(logFilePath, "Garbage"));
}
