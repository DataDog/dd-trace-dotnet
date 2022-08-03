#include "pch.h"

#include "../../src/Datadog.Tracer.Native/integration.h"

using namespace trace;

TEST(VersionStructTest, Major2GreaterThanMajor1) {
  const auto v1 = Version(1, 0, 0, 0);
  const auto v2 = Version(2, 0, 0, 0);
  ASSERT_TRUE(v2 > v1) << "Expected v2 to be greater than v1.";
}

TEST(VersionStructTest, Minor2GreaterThanMinor1) {
  const auto v0_1 = Version(0, 1, 0, 0);
  const auto v0_2 = Version(0, 2, 0, 0);
  ASSERT_TRUE(v0_2 > v0_1) << "Expected v0_2 to be greater than v0_1.";
}

TEST(VersionStructTest, Build1GreaterThanBuild0) {
  const auto v0_0_0 = Version(0, 0, 0, 0);
  const auto v0_0_1 = Version(0, 0, 1, 0);
  ASSERT_TRUE(v0_0_1 > v0_0_0) << "Expected v0_0_1 to be greater than v0_0_0.";
}

TEST(VersionStructTest, Major2GreaterThanOrEqualMajor1)
{
    const auto v1 = Version(1, 0, 0, 0);
    const auto v2 = Version(2, 0, 0, 0);
    ASSERT_TRUE(v2 >= v1) << "Expected v2 to be greater than or equal to v1.";
}

TEST(VersionStructTest, Minor2GreaterThanOrEqualMinor1)
{
    const auto v0_1 = Version(0, 1, 0, 0);
    const auto v0_2 = Version(0, 2, 0, 0);
    ASSERT_TRUE(v0_2 >= v0_1) << "Expected v0_2 to be greater than or equal to v0_1.";
}

TEST(VersionStructTest, Build1GreaterThanOrEqualBuild0)
{
    const auto v0_0_0 = Version(0, 0, 0, 0);
    const auto v0_0_1 = Version(0, 0, 1, 0);
    ASSERT_TRUE(v0_0_1 >= v0_0_0) << "Expected v0_0_1 to be greater than or equal to v0_0_0.";
}

TEST(VersionStructTest, Major1LessThanMajor2) {
  const auto v1 = Version(1, 0, 0, 0);
  const auto v2 = Version(2, 0, 0, 0);
  ASSERT_TRUE(v1 < v2) << "Expected v1 to be less than v2.";
}

TEST(VersionStructTest, Minor1LessThanMinor2) {
  const auto v0_1 = Version(0, 1, 0, 0);
  const auto v0_2 = Version(0, 2, 0, 0);
  ASSERT_TRUE(v0_1 < v0_2) << "Expected v0_1 to be less than v0_2.";
}

TEST(VersionStructTest, Build0LessThanBuild1) {
  const auto v0_0_0 = Version(0, 0, 0, 0);
  const auto v0_0_1 = Version(0, 0, 1, 0);
  ASSERT_TRUE(v0_0_0 < v0_0_1) << "Expected v0_0_0 to be less than v0_0_1.";
}

TEST(VersionStructTest, Major1LessThanOrEqualMajor2)
{
    const auto v1 = Version(1, 0, 0, 0);
    const auto v2 = Version(2, 0, 0, 0);
    ASSERT_TRUE(v1 <= v2) << "Expected v1 to be less than or equal to v2.";
}

TEST(VersionStructTest, Minor1LessThanOrEqualMinor2)
{
    const auto v0_1 = Version(0, 1, 0, 0);
    const auto v0_2 = Version(0, 2, 0, 0);
    ASSERT_TRUE(v0_1 <= v0_2) << "Expected v0_1 to be less than or equal to v0_2.";
}

TEST(VersionStructTest, Build0LessThanOrEqualBuild1)
{
    const auto v0_0_0 = Version(0, 0, 0, 0);
    const auto v0_0_1 = Version(0, 0, 1, 0);
    ASSERT_TRUE(v0_0_0 <= v0_0_1) << "Expected v0_0_0 to be less than or equal to v0_0_1.";
}

TEST(VersionStructTest, RevisionDoesNotAffectComparison) {
  const auto v1_2_3_4 = Version(1, 2, 3, 4);
  const auto v1_2_3_5 = Version(1, 2, 3, 5);
  ASSERT_FALSE(v1_2_3_5 > v1_2_3_4)
      << "Expected v1_2_3_5 to not be greater than v1_2_3_4.";
  ASSERT_FALSE(v1_2_3_4 < v1_2_3_5)
      << "Expected v1_2_3_4 to not be less than v1_2_3_5.";
}

TEST(VersionStructTest, RevisionAffectsEquality)
{
    const auto v1_2_3_4 = Version(1, 2, 3, 4);
    const auto v1_2_3_5 = Version(1, 2, 3, 5);
    ASSERT_FALSE(v1_2_3_5 == v1_2_3_4) << "Expected v1_2_3_5 to not be equal to v1_2_3_4.";
}

TEST(VersionStructTest, Equality)
{
    const auto v1_2_3_4a = Version(1, 2, 3, 4);
    const auto v1_2_3_4b = Version(1, 2, 3, 4);
    ASSERT_TRUE(v1_2_3_4a == v1_2_3_4b) << "Expected v1_2_3_4 to be equal to v1_2_3_4.";
    ASSERT_TRUE(v1_2_3_4a <= v1_2_3_4b) << "Expected v1_2_3_4 to be equal to v1_2_3_4.";
    ASSERT_TRUE(v1_2_3_4a >= v1_2_3_4b) << "Expected v1_2_3_4 to be equal to v1_2_3_4.";
    ASSERT_FALSE(v1_2_3_4a < v1_2_3_4b) << "Expected v1_2_3_4 to be equal to v1_2_3_4.";
    ASSERT_FALSE(v1_2_3_4a > v1_2_3_4b) << "Expected v1_2_3_4 to be equal to v1_2_3_4.";
}