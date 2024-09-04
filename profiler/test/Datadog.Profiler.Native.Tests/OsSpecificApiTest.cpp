// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "OsSpecificApi.h"
#include "OpSysTools.h"

#include <regex>

#ifdef _WINDOWS

TEST(OsSpecificApiTest, CheckLastErrorWithSystemMessage)
{
    const DWORD expectedErrorCode = 0;  // = success
    SetLastError(expectedErrorCode);

    auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
    ASSERT_EQ(expectedErrorCode, errorCode);
}

TEST(OsSpecificApiTest, CheckLastErrorWithoutSystemMessage)
{
    const DWORD expectedErrorCode = 123456;  // this one does not exist
    SetLastError(expectedErrorCode);

    auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
    ASSERT_EQ(expectedErrorCode, errorCode);
}

#else

#include <errno.h>

TEST(OsSpecificApiTest, CheckLastErrorMessageWithErrno)
{
    errno = 42;

    auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();

    ASSERT_EQ(42, errorCode);
}
#endif

TEST(OsSpecificApiTest, CheckProcessStartTimeFormat)
{
    auto processStartTime = OsSpecificApi::GetProcessStartTime();

    std::regex dateFormatRegex("\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z");

    ASSERT_TRUE(std::regex_match(processStartTime, dateFormatRegex));
}

TEST(OsSpecificApiTest, CheckProcessLifetime)
{
    const int seconds = 2;
    std::chrono::nanoseconds ns = std::chrono::seconds(seconds);
    OpSysTools::Sleep(ns);
    auto lifetime = OsSpecificApi::GetProcessLifetime();

    ASSERT_TRUE(lifetime >= seconds);
}