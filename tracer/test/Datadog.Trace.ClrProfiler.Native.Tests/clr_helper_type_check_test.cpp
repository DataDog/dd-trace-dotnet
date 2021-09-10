#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"
#include "test_helpers.h"

using namespace trace;

class CLRHelperTypeCheckTest : public ::CLRHelperTestBase {};


TEST_F(CLRHelperTypeCheckTest, SimpleNoSignatureMethodHasOnlyVoid) {
  std::vector<WSTRING> expected = {
      L"System.Void"};
  std::vector<std::wstring> actual;

  const auto target = FunctionToTest(
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("Silence"));

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  TryParseSignatureTypes(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTypeCheckTest, GetsVeryComplexNestedGenericTypeStrings) {
  std::vector<WSTRING> expected = {
      L"System.Void",
      L"System.String",
      L"System.Int32",
      L"System.Byte[]",
      L"System.Guid[][]",
      L"T[][][]",
      L"System.Collections.Generic.List`1<System.Byte[][]>",
      L"System.Collections.Generic.List`1<Samples.ExampleLibrary.FakeClient.DogTrick`1<T>>",
      L"System.Tuple`7<System.Int32, T, System.String, System.Object, System.Tuple`2<System.Tuple`2<T, System.Int64>, System.Int64>, System.Threading.Tasks.Task, System.Guid>",
      L"System.Collections.Generic.Dictionary`2<System.Int32, System.Collections.Generic.IList`1<System.Threading.Tasks.Task`1<Samples.ExampleLibrary.FakeClient.DogTrick`1<T>>>>"};
  std::vector<std::wstring> actual;

  const auto target = FunctionToTest(
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("Sit"));

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  TryParseSignatureTypes(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}


TEST_F(CLRHelperTypeCheckTest, SimpleStringReturnWithNestedTypeParamsNoGenerics) {
  std::vector<WSTRING> expected = {
      L"System.String", L"Samples.ExampleLibrary.FakeClient.Biscuit+Cookie",
      L"Samples.ExampleLibrary.FakeClient.Biscuit+Cookie+Raisin"};
  std::vector<std::wstring> actual;

  const auto target = FunctionToTest(
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("TellMeIfTheCookieIsYummy"));

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  TryParseSignatureTypes(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}


TEST_F(CLRHelperTypeCheckTest, SimpleClassReturnWithSimpleParamsNoGenerics) {
  std::vector<WSTRING> expected = {L"Samples.ExampleLibrary.FakeClient.Biscuit",
                                 L"System.Guid", L"System.Int16",
                                 L"Samples.ExampleLibrary.FakeClient.DogTrick"};
  std::vector<std::wstring> actual;

  const auto target = FunctionToTest(
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("Rollover"));

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  TryParseSignatureTypes(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTypeCheckTest, GenericAsyncMethodWithNestedGenericTask) {
  std::vector<WSTRING> expected = {
      L"System.Threading.Tasks.Task`1<Samples.ExampleLibrary.FakeClient.Biscuit`1<T>>",
      L"System.Guid",
      L"System.Int16",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1<T>",
      L"T",
      L"T",
  };
  std::vector<std::wstring> actual;

  const auto target = FunctionToTest(
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("StayAndLayDown"));

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  TryParseSignatureTypes(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTypeCheckTest, SuccessfullyParsesEverySignature) {
  std::set<WSTRING> expected_failures = {
      L"Samples.ExampleLibrary.Class1.ToMdArray",
      L"Samples.ExampleLibrary.Class1.ToEnumerator"
  };
  std::set<WSTRING> actual_failures;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    for (auto& method_def : EnumMethods(metadata_import_, type_def)) {
      auto target = GetFunctionInfo(metadata_import_, method_def);
      std::vector<std::wstring> actual;
      auto success = TryParseSignatureTypes(metadata_import_, target, actual);
      if (!success) {
        actual_failures.insert(target.type.name + L"." + target.name);
      }
    }
  }

  EXPECT_EQ(expected_failures, actual_failures);
}