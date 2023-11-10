// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Tags.h"

namespace libdatadog {

TEST(TagsTest, AddTag)
{
    Tags t;

    auto succeeded = t.Add("t", "v");
    ASSERT_TRUE(succeeded) << succeeded.message();
}

// Test is mainly meant for memory leak detection (unit tests are run with ASAN)
TEST(TagsTest, FailedWhenAddingEmptyTags)
{
    Tags t;
    auto succeeded = t.Add("", "");
    ASSERT_FALSE(succeeded);
}

} // namespace libdatadog
