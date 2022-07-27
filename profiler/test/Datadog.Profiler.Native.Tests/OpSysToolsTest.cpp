// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"


#include "OpSysTools.h"

// based on https://linux.die.net/man/5/proc
// -------------------------------------------
// state  = 3rd position  and 'R' for Running
// user   = 14th position in clock ticks
// kernel = 15th position in clock ticks
//

TEST(GetThreadInfoTest, Check_EmptyThreadName)
{
    std::string line = "377 () R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
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
    std::string line = "377 (ThreadNameWithoutSpace) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
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
    std::string line = "377 (Thread Name With Space) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
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
    std::string line = "377 (la (la) )) land) )) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20 ";
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
    std::string line = "377 (la (la) )) land) )) R 46 369 46 34817 369 4194368 95 0 0 0 1862 609 0 0 20)";
    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;

    bool result = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);

    ASSERT_FALSE(result);
    ASSERT_EQ(' ', state);
    ASSERT_EQ(0, userTime);
    ASSERT_EQ(0, kernelTime);
}