#include "pch.h"

#include "../../../shared/src/native-src/version.h"

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

TEST(DdVersionTests, SemanticVersionMatchesVersionProps)
{
    // DD_EXPECTED_SEMANTIC_VERSION is version.props' DD_SemanticVersion as a string literal, defined only for this
    // test project. dd_version.h macro-expands the unquoted DD_VERSION_SUFFIX before stringifying it, so a suffix
    // containing an identifier that is also a macro would report a different version, and the native/managed
    // version mismatch check would fire.
    EXPECT_STREQ(DD_EXPECTED_SEMANTIC_VERSION, DD_SEMANTIC_VERSION_STR);
    EXPECT_STREQ(DD_EXPECTED_SEMANTIC_VERSION, PROFILER_VERSION);
}
