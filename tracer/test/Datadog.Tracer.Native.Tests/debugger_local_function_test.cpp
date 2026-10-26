#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_rejit_preprocessor.h"

#include <tuple>
#include <vector>

using namespace debugger;

TEST(DebuggerLocalFunctionTest, MatchesCompilerEmittedLocalFunctionNames)
{
    // Metadata names emitted by Roslyn for local functions, with the source name a probe uses for them
    const std::vector<std::tuple<shared::WSTRING, shared::WSTRING>> localFunctions = {
        // declared in top-level statements
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("GetProduct")},
        // declared in top-level statements, capturing locals shared with a lambda (display class instance method)
        {WStr("<<Main>$>g__Captured|3"), WStr("Captured")},
        // declared in an ordinary method
        {WStr("<Handle>g__Inner|1_1"), WStr("Inner")},
        // declared in a constructor
        {WStr("<.ctor>g__Init|0_0"), WStr("Init")},
        // declared in an explicit interface implementation
        {WStr("<N.IFoo.Bar>g__Helper|2_0"), WStr("Helper")},
        // single character name
        {WStr("<Handle>g__X|4"), WStr("X")},
    };

    for (const auto& [methodName, localFunctionName] : localFunctions)
    {
        EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionNamed(methodName, localFunctionName))
            << shared::ToString(methodName) << " should match " << shared::ToString(localFunctionName);
    }
}

TEST(DebuggerLocalFunctionTest, DoesNotMatchOtherNames)
{
    const std::vector<std::tuple<shared::WSTRING, shared::WSTRING>> notLocalFunctions = {
        // a different local function in the same member
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("Product")},
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("GetProducts")},
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("GetProduc")},
        // the containing member name is not the local function name
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("Main")},
        {WStr("<Handle>g__Inner|1_1"), WStr("Handle")},
        // lambdas, state machines and closure classes have no source name
        {WStr("<<Main>$>b__0_0"), WStr("Main")},
        {WStr("<Handle>b__0"), WStr("b")},
        {WStr("MoveNext"), WStr("MoveNext")},
        {WStr("<Main>$"), WStr("Main")},
        {WStr("<Main>"), WStr("Main")},
        {WStr("<>c__DisplayClass0_0"), WStr("DisplayClass0_0")},
        // ordinary method names
        {WStr("GetProduct"), WStr("GetProduct")},
        {WStr("g__GetProduct|0_2"), WStr("GetProduct")},
        // malformed names
        {WStr("<>g__GetProduct|0_2"), WStr("GetProduct")},
        {WStr("<Main>g__GetProduct"), WStr("GetProduct")},
        {WStr("<Main>x__GetProduct|0_2"), WStr("GetProduct")},
        {WStr("<Main>g__|0_2"), WStr("")},
        {WStr(""), WStr("GetProduct")},
        {WStr("<"), WStr("GetProduct")},
        {WStr("|"), WStr("GetProduct")},
        // generated names are never accepted as a source name
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("<<Main>$>g__GetProduct|0_2")},
        {WStr("<<Main>$>g__GetProduct|0_2"), WStr("GetProduct|0_2")},
    };

    for (const auto& [methodName, localFunctionName] : notLocalFunctions)
    {
        EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionNamed(methodName, localFunctionName))
            << shared::ToString(methodName) << " should not match " << shared::ToString(localFunctionName);
    }
}

TEST(DebuggerLocalFunctionTest, AcceptsOnlySourceNames)
{
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("GetProduct")));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("X")));

    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("")));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("*")));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("<<Main>$>g__GetProduct|0_2")));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("<Main>$")));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSourceName(WStr("GetProduct|0_2")));
}

TEST(DebuggerLocalFunctionTest, MatchesTheSignatureShownToTheUser)
{
    using Types = std::vector<shared::WSTRING>;
    constexpr bool isStatic = false;
    constexpr bool isInstance = true;

    // the parameters as declared
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.String"), WStr("System.Int32")}, Types{WStr("System.String"), WStr("System.Int32")},
        isStatic));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{}, Types{}, isStatic));

    // instance local function: with or without the declaring type for 'this'
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.String")}, Types{WStr("Samples.Handlers"), WStr("System.String")}, isInstance));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{WStr("System.String")},
                                                                         Types{WStr("System.String")}, isInstance));
    EXPECT_TRUE(
        DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{}, Types{WStr("Samples.Handlers")}, isInstance));

    // locals captured in a struct closure are passed as trailing by-ref parameters the user never sees
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.Int32"), WStr("Samples.Handlers+<>c__DisplayClass1_0&")}, Types{WStr("System.Int32")},
        isStatic));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.Int32"), WStr("<>c__DisplayClass0_1&"), WStr("<>c__DisplayClass0_0&")},
        Types{WStr("System.Int32")}, isStatic));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.Int32"), WStr("Samples.Handlers+<>c__DisplayClass1_0`1[System.Int32]&")},
        Types{WStr("System.Int32")}, isStatic));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.Int32"), WStr("Samples.Handlers+<>c__DisplayClass1_0&")},
        Types{WStr("Samples.Handlers"), WStr("System.Int32")}, isInstance));
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{WStr("<>c__DisplayClass0_0&")},
                                                                         Types{}, isStatic));

    // '_' matches any type
    EXPECT_TRUE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{WStr("System.String")},
                                                                         Types{WStr("_")}, isStatic));
}

TEST(DebuggerLocalFunctionTest, DoesNotMatchAnotherSignature)
{
    using Types = std::vector<shared::WSTRING>;
    constexpr bool isStatic = false;
    constexpr bool isInstance = true;

    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{WStr("System.String")},
                                                                          Types{WStr("System.Int32")}, isStatic));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.String")}, Types{WStr("System.String"), WStr("System.Int32")}, isInstance));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.String"), WStr("System.Int32")}, Types{WStr("System.String")}, isInstance));

    // a static method has no 'this'
    EXPECT_FALSE(
        DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(Types{}, Types{WStr("System.String")}, isStatic));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.String")}, Types{WStr("Samples.Handlers"), WStr("System.String")}, isStatic));

    // only compiler-generated by-ref parameters are hidden from the user
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.Int32"), WStr("System.Int32&")}, Types{WStr("System.Int32")}, isStatic));
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("System.Int32"), WStr("<>c__DisplayClass0_0")}, Types{WStr("System.Int32")}, isStatic));
    // and only when they are trailing
    EXPECT_FALSE(DebuggerRejitPreprocessor::IsLocalFunctionSignatureMatch(
        Types{WStr("<>c__DisplayClass0_0&"), WStr("System.Int32")}, Types{WStr("System.Int32")}, isStatic));
}
