#define GTEST_LANG_CXX11 1

#include "gtest/gtest.h"
#include "../../src/Datadog.AutoInstrumentation.NativeLoader/string.h"

TEST(string, stringToString)
{
    EXPECT_TRUE(std::string("Normal String") == ToString(std::string("Normal String")));
}