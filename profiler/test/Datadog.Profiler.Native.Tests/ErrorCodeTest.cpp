// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Success.h"
#include "FfiHelper.h"

namespace libdatadog {

TEST(ErrorCodeTest, Success)
{
    auto ec = make_success();
    ASSERT_TRUE(ec);
    ASSERT_EQ(ec.message(), "");
}

TEST(ErrorCodeTest, CreateErrorCodeWithString)
{
    auto ec = make_error("Failed");
    ASSERT_FALSE(ec);
    ASSERT_EQ(ec.message(), "Failed");
}

} // namespace libdatadog