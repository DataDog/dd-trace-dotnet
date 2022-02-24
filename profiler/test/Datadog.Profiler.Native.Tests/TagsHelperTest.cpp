// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"

#include "TagsHelper.h"


TEST(TagsHelperTest, First)
{
    tags myTags = TagsHelper::Parse("");
    EXPECT_THAT(myTags, ::testing::IsEmpty());
}

TEST(TagsHelperTest, Second)
{
    tags myTags = TagsHelper::Parse("foo:bar");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", "bar"}}));
}

TEST(TagsHelperTest, Third)
{
    tags myTags = TagsHelper::Parse("foo:bar,foobar:barbar");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", "bar"}, {"foobar", "barbar"}}));
}

TEST(TagsHelperTest, Fourth)
{
    tags myTags = TagsHelper::Parse("foo");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", ""}}));
}

TEST(TagsHelperTest, Fifth)
{
    tags myTags = TagsHelper::Parse("foo:bar,");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", "bar"}}));
}

TEST(TagsHelperTest, Sixth)
{
    tags myTags = TagsHelper::Parse("foo:,");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", ""}}));
}

TEST(TagsHelperTest, Seventh)
{
    tags myTags = TagsHelper::Parse("foo,");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", ""}}));
}

TEST(TagsHelperTest, Eigth)
{
    tags myTags = TagsHelper::Parse("foo,titi:tutu");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"foo", ""}, {"titi", "tutu"}}));
}

TEST(TagsHelperTest, Nineth)
{
    tags myTags = TagsHelper::Parse(",titi:tutu");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"titi", "tutu"}}));
}

TEST(TagsHelperTest, Tenth)
{
    tags myTags = TagsHelper::Parse(",,titi:tutu");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"titi", "tutu"}}));
}

TEST(TagsHelperTest, Eleventh)
{
    tags myTags = TagsHelper::Parse("titi:tutu,,");
    EXPECT_THAT(myTags, ::testing::ContainerEq(tags{{"titi", "tutu"}}));
}
