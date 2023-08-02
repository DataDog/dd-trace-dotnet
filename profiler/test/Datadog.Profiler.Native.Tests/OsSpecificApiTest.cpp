// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "OsSpecificApi.h"

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
    DWORD errorCode;
    std::string message;
    errno = 42;
    bool hasSystemMessage = OsSpecificApi::GetLastErrorMessage(errorCode, message);

    ASSERT_EQ(42, errorCode);
}
#endif