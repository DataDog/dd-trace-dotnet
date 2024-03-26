// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "OpSysTools.h"

#include "shared/src/native-src/dd_filesystem.hpp"

// based on https://linux.die.net/man/5/proc
// -------------------------------------------
// state  = 3rd position  and 'R' for Running
// user   = 14th position in clock ticks
// kernel = 15th position in clock ticks
//

TEST(GetThreadInfoTest, Check_EmptyThreadName)
{
    auto const* line = "377 () R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_TRUE(result);
    ASSERT_EQ('R', state);
    ASSERT_EQ(1862, userTime);
    ASSERT_EQ(609, kernelTime);
}

TEST(GetThreadInfoTest, Check_NoSpaceThreadName)
{
    auto const* line = "377 (ThreadNameWithoutSpace) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_TRUE(result);
    ASSERT_EQ('R', state);
    ASSERT_EQ(1862, userTime);
    ASSERT_EQ(609, kernelTime);
}

TEST(GetThreadInfoTest, Check_SpaceInThreadName)
{
    auto const* line = "377 (Thread Name With Space) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_TRUE(result);
    ASSERT_EQ('R', state);
    ASSERT_EQ(1862, userTime);
    ASSERT_EQ(609, kernelTime);
}

TEST(GetThreadInfoTest, Check_SpaceAndParenthesisInThreadName)
{
    auto const* line = "377 (la (la) )) land) )) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_TRUE(result);
    ASSERT_EQ('R', state);
    ASSERT_EQ(1862, userTime);
    ASSERT_EQ(609, kernelTime);
}

TEST(GetThreadInfoTest, Check_SpaceAndParenthesisInThreadName_And_FinalParenthesis)
{
    auto const* line = "377 (la (la) )) land) )) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20)";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
    ASSERT_EQ(' ', state);
    ASSERT_EQ(0, userTime);
    ASSERT_EQ(0, kernelTime);
}

TEST(GetThreadInfoTest, Check_MalformedString1)
{
    auto const* line = "377 (dotnet)";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);;
}

TEST(GetThreadInfoTest, Check_MalformedString_NoParen_Around_ThreadName)
{
    auto const* line = "377 dotnet R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
}

TEST(GetThreadInfoTest, Check_EmptyString)
{
    auto const* line = "";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
}

TEST(GetThreadInfoTest, Check_Missing_User_KernelTime)
{
    auto const* line = "377 (dotnet) R";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
}

TEST(GetThreadInfoTest, Check_Missing_User_KernelTime2)
{
    auto const* line = "377 (dotnet) R 46 369 46 34817";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
}

TEST(GetThreadInfoTest, Check_Missing_KernelTime)
{
    auto const* line = "377 (dotnet) R 46 369 46 34817 369 4194368 95 0 0 0 1862";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
}

#endif