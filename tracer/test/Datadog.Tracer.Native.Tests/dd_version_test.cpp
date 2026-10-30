#include "pch.h"

#include "../../../shared/src/native-src/dd_version.h"

#define DD_TEST_EMPTY_SUFFIX
#define DD_TEST_PRERELEASE_SUFFIX -prerelease
#define DD_TEST_DEV_SUFFIX -dev.123

TEST(DdVersionTests, SuffixStringification)
{
    // DD_VERSION_SUFFIX is an unquoted (possibly empty) token sequence, so check that empty and non-empty suffixes stringify exactly
    EXPECT_EQ(std::string(""), DD_STRINGIFY_VA(DD_TEST_EMPTY_SUFFIX));
    EXPECT_EQ(std::string("-prerelease"), DD_STRINGIFY_VA(DD_TEST_PRERELEASE_SUFFIX));
    EXPECT_EQ(std::string("-dev.123"), DD_STRINGIFY_VA(DD_TEST_DEV_SUFFIX));
}
