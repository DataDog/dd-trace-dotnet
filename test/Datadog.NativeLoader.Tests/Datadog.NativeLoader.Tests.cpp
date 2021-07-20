// Datadog.NativeLoader.Tests.cpp : This file contains the 'main' function. Program execution begins and ends there.
//
#define GTEST_LANG_CXX11 1

#include "gtest/gtest.h"

// Demonstrate some basic assertions.
TEST(HelloTest, BasicAssertions)
{
    // Expect two strings not to be equal.
    EXPECT_STRNE("hello", "world");
    // Expect equality.
    EXPECT_EQ(7 * 6, 42);
}