#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_constants.h"
#include "../../src/Datadog.Tracer.Native/debugger_method_rewriter.h"
#include "../../src/Datadog.Tracer.Native/runtime_async.h"
#include "test_helpers.h"

#include <vector>

using namespace trace;
using namespace debugger;

class DebuggerRuntimeAsyncTest : public ::CLRHelperTestBase
{
protected:
    const RuntimeInformation net11_{COR_PRF_CORE_CLR, 11, 0, 0, 0};

    // async Task<Biscuit<T1>> StayAndLayDown<TM1, TM2>(...), an instance method on a class
    FunctionInfo RuntimeAsyncTaskOfTMethod() const
    {
        auto function = FunctionToTest(WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("StayAndLayDown"));
        EXPECT_TRUE(function.IsValid());
        EXPECT_EQ(S_OK, function.method_signature.TryParse());
        function.method_impl_flags |= miAsync;
        return function;
    }
};

TEST_F(DebuggerRuntimeAsyncTest, AcceptsTaskOfTOnNet11AndReturnsTheUnwrappedType)
{
    const auto function = RuntimeAsyncTaskOfTMethod();

    TypeSignature effective = function.method_signature.GetReturnValue();
    EXPECT_EQ(nullptr,
              DebuggerMethodRewriter::GetRuntimeAsyncDeclineReason(function, metadata_import_, net11_, &effective));

    // Biscuit<T1>, not the declared Task<Biscuit<T1>>
    EXPECT_EQ(static_cast<unsigned>(ELEMENT_TYPE_GENERICINST), std::get<0>(effective.GetElementTypeAndFlags()));
    EXPECT_LT(effective.length, function.method_signature.GetReturnValue().length);
}

TEST_F(DebuggerRuntimeAsyncTest, DeclinesBeforeNet11)
{
    const auto function = RuntimeAsyncTaskOfTMethod();
    const RuntimeInformation runtimes[] = {
        {COR_PRF_CORE_CLR, 10, 0, 0, 0},
        {COR_PRF_DESKTOP_CLR, 4, 0, 0, 0},
        {COR_PRF_DESKTOP_CLR, 11, 0, 0, 0},
    };

    for (const auto& runtime : runtimes)
    {
        TypeSignature effective = function.method_signature.GetReturnValue();
        const auto declared = effective;
        const auto* reason =
            DebuggerMethodRewriter::GetRuntimeAsyncDeclineReason(function, metadata_import_, runtime, &effective);
        ASSERT_NE(nullptr, reason) << "major_version=" << runtime.major_version;
        EXPECT_EQ(invalid_probe_runtime_async_requires_net11, *reason);
        EXPECT_EQ(declared.pbBase, effective.pbBase);
        EXPECT_EQ(declared.length, effective.length);
    }
}

TEST_F(DebuggerRuntimeAsyncTest, DeclinesNonTaskReturns)
{
    const std::vector<shared::WSTRING> methods = {WStr("Silence"), WStr("TellMeIfTheCookieIsYummy"), WStr("Rollover")};
    for (const auto& method_name : methods)
    {
        auto function = FunctionToTest(WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), method_name);
        ASSERT_TRUE(function.IsValid());
        ASSERT_EQ(S_OK, function.method_signature.TryParse());
        function.method_impl_flags |= miAsync;

        const auto* reason = DebuggerMethodRewriter::GetRuntimeAsyncDeclineReason(function, metadata_import_, net11_);
        ASSERT_NE(nullptr, reason) << "Failed method is : " << shared::ToString(method_name);
        EXPECT_EQ(invalid_probe_runtime_async_unsupported_return_type, *reason);
    }
}

TEST_F(DebuggerRuntimeAsyncTest, DeclinesInstanceMethodsOnAStruct)
{
    // The sample library has no Task-returning struct method, so put a real Task<T> instance method on a struct.
    const auto task_method = RuntimeAsyncTaskOfTMethod();
    const auto struct_method = FunctionToTest(WStr("Samples.ExampleLibrary.GenericTests.PointStruct"), WStr(".ctor"));
    ASSERT_TRUE(struct_method.IsValid());
    ASSERT_TRUE(struct_method.type.valueType);

    const FunctionInfo function(task_method.id, task_method.name, struct_method.type, task_method.signature,
                                task_method.method_signature, task_method.method_impl_flags);

    const auto* reason = DebuggerMethodRewriter::GetRuntimeAsyncDeclineReason(function, metadata_import_, net11_);
    ASSERT_NE(nullptr, reason);
    EXPECT_EQ(invalid_probe_runtime_async_value_type_not_supported, *reason);
}

TEST_F(DebuggerRuntimeAsyncTest, IgnoresMethodsThatAreNotRuntimeAsync)
{
    auto function = RuntimeAsyncTaskOfTMethod();
    function.method_impl_flags &= ~miAsync;

    TypeSignature effective = function.method_signature.GetReturnValue();
    const auto declared = effective;
    const RuntimeInformation net8{COR_PRF_CORE_CLR, 8, 0, 0, 0};
    EXPECT_EQ(nullptr, DebuggerMethodRewriter::GetRuntimeAsyncDeclineReason(function, metadata_import_, net8, &effective));
    EXPECT_EQ(declared.pbBase, effective.pbBase);
    EXPECT_EQ(declared.length, effective.length);
}
