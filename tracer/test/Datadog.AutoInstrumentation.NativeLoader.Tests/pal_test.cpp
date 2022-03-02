#include "gtest/gtest.h"
#include "../../../shared/src/native-src/pal.h"

using namespace shared;

TEST(pal, EnvironmentVariables)
{
    EXPECT_TRUE(WStr("Windows_NT") == GetEnvironmentValue(WStr("OS")));
    EXPECT_TRUE(GetEnvironmentValues(WStr("PATH")).size() > 0);

    bool isSet = SetEnvironmentValue(WStr("CUSTOM_ENV_VAR_KEY"), WStr("CUSTOM_ENV_VAR_VALUE"));
    EXPECT_TRUE(isSet);
    EXPECT_TRUE(GetEnvironmentValue(WStr("CUSTOM_ENV_VAR_KEY")) == WStr("CUSTOM_ENV_VAR_VALUE"));

    isSet = SetEnvironmentValue(WStr("CUSTOM_ENV_VAR_KEY"), WStr("CUSTOM_ENV_VAR_VALUE1;CUSTOM_ENV_VAR_VALUE2;CUSTOM_ENV_VAR_VALUE3"));
    EXPECT_TRUE(isSet);
    std::vector<WSTRING> values = GetEnvironmentValues(WStr("CUSTOM_ENV_VAR_KEY"), ';');
    EXPECT_EQ(3, values.size());
    EXPECT_TRUE(WStr("CUSTOM_ENV_VAR_VALUE1") == values[0]);
    EXPECT_TRUE(WStr("CUSTOM_ENV_VAR_VALUE2") == values[1]);
    EXPECT_TRUE(WStr("CUSTOM_ENV_VAR_VALUE3") == values[2]);
}