#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"
#include "test_helpers.h"

using namespace trace;

class CLRHelperTypeCheckTest : public ::CLRHelperTestBase {};


TEST_F(CLRHelperTypeCheckTest, SimpleNoSignatureMethodHasOnlyVoid) {
  std::list<WSTRING> expected = {
      L"System.Void"};
  std::list<std::wstring> actual;

  const auto target = FunctionToTest(
      "Samples.ExampleLibrary.FakeClient.DogClient`2"_W, "Silence"_W);

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  SignatureFuzzyMatch(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTypeCheckTest, GetsVeryComplexNestedGenericTypeStrings) {
  std::list<WSTRING> expected = {
      L"System.Void",
      L"System.String",
      L"System.Int32",
      L"System.UInt8[]",
      L"System.Guid[][]",
      L"T[][][]",
      L"System.Collections.Generic.List`1<System.UInt8[][]>",
      L"System.Collections.Generic.List`1<Samples.ExampleLibrary.FakeClient.DogTrick`1<T>>",
      L"System.Tuple`7<System.Int32, T, System.String, System.Object, System.Tuple`2<System.Tuple`2<T, System.Int64>, System.Int64>, System.Threading.Tasks.Task, System.Guid>",
      L"System.Collections.Generic.Dictionary`2<System.Int32, System.Collections.Generic.IList`1<System.Threading.Tasks.Task`1<Samples.ExampleLibrary.FakeClient.DogTrick`1<T>>>>"};
  std::list<std::wstring> actual;

  const auto target = FunctionToTest(
      "Samples.ExampleLibrary.FakeClient.DogClient`2"_W, "Sit"_W);

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  SignatureFuzzyMatch(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTypeCheckTest, SimpleClassReturnWithSimpleParamsNoGenerics) {
  std::list<WSTRING> expected = {L"Samples.ExampleLibrary.FakeClient.Biscuit",
                                 L"System.Guid", L"System.Int16",
                                 L"Samples.ExampleLibrary.FakeClient.DogTrick"};
  std::list<std::wstring> actual;

  const auto target = FunctionToTest(
      "Samples.ExampleLibrary.FakeClient.DogClient`2"_W, "Rollover"_W);

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  SignatureFuzzyMatch(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTypeCheckTest, GenericAsyncMethodWithNestedGenericTask) {
  std::list<WSTRING> expected = {
      L"System.Threading.Tasks.Task`1<Samples.ExampleLibrary.FakeClient.Biscuit`1<T>>",
      L"System.Guid",
      L"System.Int16",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1<T>",
      L"T",
      L"T",
  };
  std::list<std::wstring> actual;

  const auto target = FunctionToTest(
      "Samples.ExampleLibrary.FakeClient.DogClient`2"_W, "StayAndLayDown"_W);

  EXPECT_TRUE(target.name.size() > 1) << "Test target method not found.";

  SignatureFuzzyMatch(metadata_import_, target, actual);

  EXPECT_EQ(expected, actual);
}