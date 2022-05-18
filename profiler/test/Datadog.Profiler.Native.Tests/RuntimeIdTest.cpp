// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "shared/src/native-src/util.h"

#include <string>
#include <unordered_set>

TEST(RuntimeIdTest, EnsureRuntimeIdIsUUIDFormat)
{
    auto runtimeId = ::shared::GenerateRuntimeId();

    // UUID format
    // Ensure the string is 36-character long
    // XXXXXXXX-XXXX-4XXX-XXXX-XXXXXXXXXXXX
    ASSERT_EQ(36, runtimeId.size());

    // Ensure we have the '-'
    // XXXXXXXX-XXXX-4XXX-XXXX-XXXXXXXXXXXX
    //         ^    ^    ^    ^
    //         |    |    |    |

    ASSERT_EQ('-', runtimeId[8]);
    ASSERT_EQ('-', runtimeId[13]);
    ASSERT_EQ('-', runtimeId[18]);
    ASSERT_EQ('-', runtimeId[23]);

    // Ensure we have the '4' for the version
    // XXXXXXXX-XXXX-4XXX-XXXX-XXXXXXXXXXXX
    //               ^

    ASSERT_EQ('4', runtimeId[14]);

    // Ensure all the number is in hex format
    // XXXXXXXX-XXXX-4XXX-XXXX-XXXXXXXXXXXX

    for (auto c : runtimeId)
    {
        if (c == '-')
        {
            continue;
        }

        ASSERT_TRUE(std::isxdigit(c));
    }
}

TEST(RuntimeIdTest, EnsureRuntimeIdIsDifferentFromDifferentCalls)
{
    std::unordered_set<std::string> runtimeIds;

    for (auto i = 0; i < 20; i++) // for now only 20 is enough for our usage
    {
        auto it = runtimeIds.insert(::shared::GenerateRuntimeId());

        // check that the string was added to the set
        ASSERT_TRUE(it.second);
    }
}