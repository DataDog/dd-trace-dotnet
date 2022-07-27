// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end
#include <tuple>

#include "HResultConverter.h"

namespace HResultConverterTest {
class HResultConverterParametersTests : public ::testing::TestWithParam<std::tuple<int, const char*>>
{
};

TEST_P(HResultConverterParametersTests, CheckMessageForHresult)
{
    auto [code, expectedMessage] = GetParam();
    EXPECT_STREQ(expectedMessage, HResultConverter::ToChars(code));
}

INSTANTIATE_TEST_SUITE_P(
    HResultConverterTests,
    HResultConverterParametersTests,
    ::testing::Values(
        std::make_tuple(CORPROF_E_STACKSNAPSHOT_ABORTED, HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_ABORTED),
        std::make_tuple(CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD, HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD),
        std::make_tuple(CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX, HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX),
        std::make_tuple(CORPROF_E_STACKSNAPSHOT_UNSAFE, HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNSAFE),
        std::make_tuple(CORPROF_E_INCONSISTENT_WITH_FLAGS, HResultConverter::hrCodeNameStr_CORPROF_E_INCONSISTENT_WITH_FLAGS),
        std::make_tuple(CORPROF_E_UNSUPPORTED_CALL_SEQUENCE, HResultConverter::hrCodeNameStr_CORPROF_E_UNSUPPORTED_CALL_SEQUENCE),
        std::make_tuple(E_INVALIDARG, HResultConverter::hrCodeNameStr_E_INVALIDARG),
        std::make_tuple(E_FAIL, HResultConverter::hrCodeNameStr_E_FAIL),
        std::make_tuple(S_FALSE, HResultConverter::hrCodeNameStr_S_FALSE),
        std::make_tuple(S_OK, HResultConverter::hrCodeNameStr_S_OK),
        std::make_tuple(-42, HResultConverter::hrCodeNameStr_UnspecifiedFail),
        std::make_tuple(42, HResultConverter::hrCodeNameStr_UnspecifiedSuccess)));

} // namespace HResultConverterTest