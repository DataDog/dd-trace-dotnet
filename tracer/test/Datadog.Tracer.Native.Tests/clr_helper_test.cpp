#include "pch.h"

#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "../../src/Datadog.Tracer.Native/runtime_async.h"
#include "signature_test_helpers.h"
#include "test_helpers.h"
#include "../../../shared/src/native-src/pal.h"

#include <tuple>
#include <vector>

using namespace trace;

class CLRHelperTest : public ::CLRHelperTestBase {};

TEST_F(CLRHelperTest, EnumeratesTypeDefs) {
  std::vector<shared::WSTRING> expected_types = {
      WStr("Samples.ExampleLibrary.Class1"),
      WStr("Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2"),
      WStr("Samples.ExampleLibrary.GenericTests.GenericTarget`2"),
      WStr("Samples.ExampleLibrary.GenericTests.PointStruct"),
      WStr("Samples.ExampleLibrary.GenericTests.StructContainer`1"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit`1"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit"),
      WStr("Samples.ExampleLibrary.FakeClient.StructBiscuit"),
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick`1"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick"),
      WStr("<>c"),
      WStr("Cookie"),
      WStr("Cookie"),
      WStr("<StayAndLayDown>d__4`2"),
      WStr("Raisin")};

  std::vector<shared::WSTRING> actual_types;

  for (auto& def : EnumTypeDefs(metadata_import_)) {
    shared::WSTRING name(256, 0);
    DWORD name_sz = 0;
    DWORD flags = 0;
    mdToken extends = 0;
    auto hr = metadata_import_->GetTypeDefProps(
        def, name.data(), (DWORD)(name.size()), &name_sz, &flags, &extends);
    ASSERT_TRUE(SUCCEEDED(hr));

    if (name_sz > 0) {
      name = name.substr(0, name_sz - 1);
      actual_types.push_back(name);
    }
  }

  EXPECT_EQ(expected_types, actual_types);
}

TEST_F(CLRHelperTest, EnumeratesAssemblyRefs) {
  std::vector<shared::WSTRING> expected_assemblies = {
      WStr("System.Runtime"),
      WStr("System.Collections"),
      WStr("System.Threading.Tasks"),
      WStr("System.Diagnostics.Debug")};
  std::vector<shared::WSTRING> actual_assemblies;
  for (auto& ref : EnumAssemblyRefs(assembly_import_)) {
    auto name = GetReferencedAssemblyMetadata(assembly_import_, ref).name;
    if (!name.empty()) {
      actual_assemblies.push_back(name);
    }
  }
  EXPECT_EQ(expected_assemblies, actual_assemblies);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromTypeDefs) {
  std::set<shared::WSTRING> expected = {
      WStr("<>c"),
      WStr("<StayAndLayDown>d__4`2"),
      WStr("Cookie"),
      WStr("Raisin"),
      WStr("Samples.ExampleLibrary.Class1"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit`1"),
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick`1"),
      WStr("Samples.ExampleLibrary.FakeClient.StructBiscuit"),
      WStr("Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2"),
      WStr("Samples.ExampleLibrary.GenericTests.GenericTarget`2"),
      WStr("Samples.ExampleLibrary.GenericTests.PointStruct"),
      WStr("Samples.ExampleLibrary.GenericTests.StructContainer`1")};
  std::set<shared::WSTRING> actual;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_def);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromTypeRefs) {
  std::set<shared::WSTRING> expected = {
      WStr("DebuggingModes"),
      WStr("Enumerator"),
      WStr("System.Array"),
      WStr("System.Collections.DictionaryEntry"),
      WStr("System.Collections.Generic.Dictionary`2"),
      WStr("System.Collections.Generic.IList`1"),
      WStr("System.Collections.Generic.List`1"),
      WStr("System.Diagnostics.DebuggableAttribute"),
#ifdef _DEBUG
      WStr("System.Diagnostics.DebuggerBrowsableAttribute"),
      WStr("System.Diagnostics.DebuggerBrowsableState"),
#endif
      WStr("System.Diagnostics.DebuggerHiddenAttribute"),
#ifdef _DEBUG
      WStr("System.Diagnostics.DebuggerStepThroughAttribute"),
#endif
      WStr("System.Exception"),
      WStr("System.Func`3"),
      WStr("System.Guid"),
      WStr("System.Int32"),
      WStr("System.Object"),
      WStr("System.Reflection.AssemblyCompanyAttribute"),
      WStr("System.Reflection.AssemblyConfigurationAttribute"),
      WStr("System.Reflection.AssemblyFileVersionAttribute"),
      WStr("System.Reflection.AssemblyInformationalVersionAttribute"),
      WStr("System.Reflection.AssemblyProductAttribute"),
      WStr("System.Reflection.AssemblyTitleAttribute"),
      WStr("System.Runtime.CompilerServices.AsyncStateMachineAttribute"),
      WStr("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1"),
      WStr("System.Runtime.CompilerServices.CompilationRelaxationsAttribute"),
      WStr("System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
      WStr("System.Runtime.CompilerServices.IAsyncStateMachine"),
      WStr("System.Runtime.CompilerServices.RuntimeCompatibilityAttribute"),
      WStr("System.Runtime.CompilerServices.TaskAwaiter"),
      WStr("System.Runtime.CompilerServices.TaskAwaiter`1"),
      WStr("System.Runtime.Versioning.TargetFrameworkAttribute"),
      WStr("System.RuntimeTypeHandle"),
      WStr("System.String"),
      WStr("System.Threading.Tasks.Task"),
      WStr("System.Threading.Tasks.Task`1"),
      WStr("System.Tuple`2"),
      WStr("System.Tuple`7"),
      WStr("System.Type"),
      WStr("System.ValueType")};
  std::set<shared::WSTRING> actual;
  for (auto& type_ref : EnumTypeRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_ref);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromTypeSpecs)
{
    std::set<shared::WSTRING> expected = {
	WStr("<StayAndLayDown>d__4`2"), 
	WStr("Samples.ExampleLibrary.Class1"), 
	WStr("Samples.ExampleLibrary.FakeClient.Biscuit`1"), 
	WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), 
	WStr("Samples.ExampleLibrary.FakeClient.DogTrick`1"), 
	WStr("Samples.ExampleLibrary.GenericTests.GenericTarget`2"), 
	WStr("Samples.ExampleLibrary.GenericTests.StructContainer`1"),
	WStr("System.Collections.Generic.List`1"),
	WStr("System.Func`3"),
	WStr("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1"),
	WStr("System.Runtime.CompilerServices.TaskAwaiter`1"),
	WStr("System.Threading.Tasks.Task`1") };
  std::set<shared::WSTRING> actual;
  for (auto& type_def : EnumTypeSpecs(metadata_import_))
  {
    auto type_info = GetTypeInfo(metadata_import_, type_def);
    if (type_info.IsValid())
    {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromModuleRefs) {
  // TODO(cbd): figure out how to create a module ref, for now its empty
  std::set<shared::WSTRING> expected = {};
  std::set<shared::WSTRING> actual;
  for (auto& module_ref : EnumModuleRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, module_ref);
    actual.insert(type_info.name);
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromMethods) {
  std::set<shared::WSTRING> expected = {
      WStr("<>c"),
      WStr("<StayAndLayDown>d__4`2"),
      WStr("Cookie"),
      WStr("Raisin"),
      WStr("Samples.ExampleLibrary.Class1"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit`1"),
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick`1"),
      WStr("Samples.ExampleLibrary.FakeClient.StructBiscuit"),
      WStr("Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2"),
      WStr("Samples.ExampleLibrary.GenericTests.GenericTarget`2"),
      WStr("Samples.ExampleLibrary.GenericTests.PointStruct"),
      WStr("Samples.ExampleLibrary.GenericTests.StructContainer`1")};
  std::set<shared::WSTRING> actual;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    for (auto& method_def : EnumMethods(metadata_import_, type_def)) {
      auto type_info = GetTypeInfo(metadata_import_, method_def);
      if (type_info.IsValid()) {
        actual.insert(type_info.name);
      }
    }
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FindTypeDefsByName) {
  std::vector<shared::WSTRING> expected_types = {
      WStr("Samples.ExampleLibrary.Class1"),
      WStr("Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2"),
      WStr("Samples.ExampleLibrary.GenericTests.GenericTarget`2"),
      WStr("Samples.ExampleLibrary.GenericTests.PointStruct"),
      WStr("Samples.ExampleLibrary.GenericTests.StructContainer`1"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit`1"),
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit"),
      WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick`1"),
      WStr("Samples.ExampleLibrary.FakeClient.DogTrick")};

  for (auto& def : expected_types) {
    mdTypeDef typeDef = mdTypeDefNil;
    auto found = FindTypeDefByName(def, WStr("Samples.ExampleLibrary"),
                                   metadata_import_, typeDef);
    EXPECT_TRUE(found) << "Failed type is : " << shared::ToString(def) << std::endl;
    EXPECT_NE(typeDef, mdTypeDefNil) << "Failed type is : " << shared::ToString(def) << std::endl;
  }
}

TEST_F(CLRHelperTest, FindNestedTypeDefsByName) {
  std::vector<shared::WSTRING> expected_types = {
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit+Cookie"),
      WStr("Samples.ExampleLibrary.FakeClient.StructBiscuit+Cookie")};

  for (auto& def : expected_types) {
    mdTypeDef typeDef = mdTypeDefNil;
    auto found = FindTypeDefByName(def, WStr("Samples.ExampleLibrary"),
                                   metadata_import_, typeDef);
    EXPECT_TRUE(found) << "Failed type is : " << shared::ToString(def) << std::endl;
    EXPECT_NE(typeDef, mdTypeDefNil) << "Failed type is : " << shared::ToString(def) << std::endl;
  }
}

TEST_F(CLRHelperTest, FindDoubleNestedTypeDefsByName) {
  std::vector<shared::WSTRING> expected_types = {
      WStr("Samples.ExampleLibrary.FakeClient.Biscuit+Cookie+Raisin")};

  for (auto& def : expected_types) {
    mdTypeDef typeDef = mdTypeDefNil;
    auto found = FindTypeDefByName(def, WStr("Samples.ExampleLibrary"),
                                   metadata_import_, typeDef);
    EXPECT_TRUE(found) << "Failed type is : " << shared::ToString(def) << std::endl;
    EXPECT_NE(typeDef, mdTypeDefNil) << "Failed type is : " << shared::ToString(def) << std::endl;
  }
}

TEST_F(CLRHelperTest, DoesNotFindDoubleNestedTypeDefsByName)
{
    std::vector<shared::WSTRING> expected_types = {WStr("Samples.ExampleLibrary.NotARealClass")};

    for (auto& def : expected_types)
    {
        mdTypeDef typeDef = mdTypeDefNil;
        auto found = FindTypeDefByName(def, WStr("Samples.ExampleLibrary"), metadata_import_, typeDef);
        EXPECT_FALSE(found) << "Failed type is : " << shared::ToString(def) << std::endl;
        EXPECT_EQ(typeDef, mdTypeDefNil) << "Failed type is : " << shared::ToString(def) << std::endl;
    }
}

TEST_F(CLRHelperTest, TypeSignatureGetTypeTokName) {
  COR_SIGNATURE signatureChar[] = {ELEMENT_TYPE_CHAR};
  TypeSignature charTypeSignature{};
  charTypeSignature.pbBase = signatureChar;
  charTypeSignature.length = 1;
  charTypeSignature.offset = 0;

  COR_SIGNATURE signatureByRefChar[] = {ELEMENT_TYPE_BYREF, ELEMENT_TYPE_CHAR};
  TypeSignature byRefCharTypeSignature{};
  byRefCharTypeSignature.pbBase = signatureByRefChar;
  byRefCharTypeSignature.length = 2;
  byRefCharTypeSignature.offset = 0;

  COR_SIGNATURE signaturePtrChar[] = {ELEMENT_TYPE_PTR, ELEMENT_TYPE_CHAR};
  TypeSignature ptrCharTypeSignature{};
  ptrCharTypeSignature.pbBase = signaturePtrChar;
  ptrCharTypeSignature.length = 2;
  ptrCharTypeSignature.offset = 0;

  std::vector<std::tuple<TypeSignature, shared::WSTRING>> tests = {
        {charTypeSignature, WStr("System.Char")},
        {byRefCharTypeSignature, WStr("System.Char&")},
        {ptrCharTypeSignature, WStr("System.Char*")}};

  for (auto& test : tests) {
    auto actual = std::get<0>(test).GetTypeTokName(metadata_import_);
    auto expected = std::get<1>(test);

    EXPECT_EQ(actual, expected);
  }
}

TEST_F(CLRHelperTest, TypeSignatureMayBeByRefLike) {
  const auto mayBeByRefLike = [](std::initializer_list<COR_SIGNATURE> signature) {
    const std::vector<COR_SIGNATURE> signatureBytes(signature);
    TypeSignature typeSignature{0, static_cast<ULONG>(signatureBytes.size()), signatureBytes.data()};
    return typeSignature.MayBeByRefLike();
  };

  EXPECT_TRUE(mayBeByRefLike({ELEMENT_TYPE_VALUETYPE}));
  EXPECT_TRUE(mayBeByRefLike({ELEMENT_TYPE_TYPEDBYREF}));
  EXPECT_TRUE(mayBeByRefLike({ELEMENT_TYPE_GENERICINST, ELEMENT_TYPE_VALUETYPE}));

  EXPECT_FALSE(mayBeByRefLike({ELEMENT_TYPE_GENERICINST, ELEMENT_TYPE_CLASS}));
  EXPECT_FALSE(mayBeByRefLike({ELEMENT_TYPE_VOID}));
  EXPECT_FALSE(mayBeByRefLike({ELEMENT_TYPE_I4}));
  EXPECT_FALSE(mayBeByRefLike({ELEMENT_TYPE_STRING}));
  EXPECT_FALSE(mayBeByRefLike({ELEMENT_TYPE_OBJECT}));
  EXPECT_FALSE(mayBeByRefLike({ELEMENT_TYPE_CLASS}));
}

TEST_F(CLRHelperTest, FunctionLocalSignatureTryParse) {
  COR_SIGNATURE localSignatureWithOneChar[] = {0x07, 0x01, ELEMENT_TYPE_CHAR};
  COR_SIGNATURE localSignatureWithOneByRefChar[] = {0x07, 0x01, ELEMENT_TYPE_BYREF, ELEMENT_TYPE_CHAR};
  COR_SIGNATURE localSignatureWithOnePtrChar[] = {0x07, 0x01, ELEMENT_TYPE_PTR, ELEMENT_TYPE_CHAR};

  std::vector<std::tuple<COR_SIGNATURE*, ULONG, shared::WSTRING>> tests = {
        {localSignatureWithOneChar, 3, WStr("[System.Char]")},
        {localSignatureWithOneByRefChar, 4, WStr("[System.Char&]")},
        {localSignatureWithOnePtrChar, 4, WStr("[System.Char*]")}};

  for (auto& test : tests) {
    std::vector<TypeSignature> locals;
    HRESULT hr = FunctionLocalSignature::TryParse(std::get<0>(test), std::get<1>(test), locals);
    EXPECT_EQ(hr, S_OK);
    EXPECT_EQ(locals.size(), 1) << "Failed test input is params=" << std::get<2>(test) << std::endl;
  }
}

// GetFunctionInfo reads the impl flags out of one of GetMemberProps' thirteen out-params. Getting
// that argument position wrong would silently hand us the code RVA (or nothing) instead, so this
// cross-checks every method in the sample library against an independent GetMethodProps call.
TEST_F(CLRHelperTest, GetFunctionInfoPopulatesMethodImplFlags) {
  size_t methodsChecked = 0;

  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    for (auto& method_def : EnumMethods(metadata_import_, type_def)) {
      DWORD expected_impl_flags = 0;
      auto hr = metadata_import_->GetMethodProps(method_def, nullptr, nullptr, 0, nullptr, nullptr,
                                                 nullptr, nullptr, nullptr, &expected_impl_flags);
      ASSERT_TRUE(SUCCEEDED(hr));

      const auto function_info = GetFunctionInfo(metadata_import_, method_def);
      ASSERT_TRUE(function_info.IsValid());

      EXPECT_EQ(expected_impl_flags, function_info.method_impl_flags)
          << "Failed method is : " << shared::ToString(function_info.name) << std::endl;

      // The sample library is ordinary C#, so every method is plain IL and none is runtime-async.
      EXPECT_TRUE(IsMiIL(function_info.method_impl_flags));
      EXPECT_FALSE(IsMiAsync(function_info.method_impl_flags));

      methodsChecked++;
    }
  }

  EXPECT_GT(methodsChecked, 0u);
}

// IsMiAsync itself is covered by RuntimeAsyncTest.IsMiAsyncMatchesTheAsyncBit, which builds on
// every platform. This file does not, so only the metadata-backed cases belong here.

namespace {

// Resolves a TypeRef by name from the sample library, or defines one if it has none - ValueTask
// does not exist in netstandard1.0, so there is nothing to find for those. Synthesised refs reuse
// the resolution scope of a ref the assembly really has, so the token we hand to
// TryGetRuntimeAsyncEffectiveReturnType resolves exactly like a real one.
mdTypeRef GetOrDefineTypeRef(const ComPtr<IMetaDataImport2>& metadata_import,
                             const ComPtr<IMetaDataEmit2>& metadata_emit,
                             const shared::WSTRING& target_name) {
  mdToken fallback_scope = mdTokenNil;

  for (auto& type_ref : EnumTypeRefs(metadata_import)) {
    shared::WSTRING name(512, 0);
    ULONG name_sz = 0;
    mdToken resolution_scope = mdTokenNil;
    if (FAILED(metadata_import->GetTypeRefProps(type_ref, &resolution_scope, name.data(),
                                                (DWORD)(name.size()), &name_sz)) ||
        name_sz == 0) {
      continue;
    }

    name = name.substr(0, name_sz - 1);
    if (name == target_name) {
      return type_ref;
    }

    if (fallback_scope == mdTokenNil && name.rfind(WStr("System.Threading.Tasks."), 0) == 0) {
      fallback_scope = resolution_scope;
    }
  }

  mdTypeRef defined = mdTypeRefNil;
  if (FAILED(metadata_emit->DefineTypeRefByName(fallback_scope, target_name.c_str(), &defined))) {
    return mdTypeRefNil;
  }

  return defined;
}

void ExpectEffectiveTypeIsVoid(const TypeSignature& effective) {
  const auto [element_type, type_flags] = effective.GetElementTypeAndFlags();

  EXPECT_EQ(static_cast<unsigned>(ELEMENT_TYPE_VOID), element_type);

  // Exact equality, not a mask: CallTargetTokens::ModifyLocalSig tests `retTypeFlags !=
  // TypeFlagVoid`, so a stray PTR/PINNED/BYREF prefix on the substituted signature would make it
  // allocate a return local for a method that has no return value.
  EXPECT_EQ(TypeFlagVoid, type_flags);
}

}  // namespace

TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeForTaskIsVoid) {
  const auto task = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                       WStr("System.Threading.Tasks.Task"));
  ASSERT_NE(mdTypeRefNil, task);

  const auto bytes = SigBuilder().Byte(ELEMENT_TYPE_CLASS).Token(task).Bytes();

  TypeSignature effective{};
  ASSERT_TRUE(TryGetRuntimeAsyncEffectiveReturnType(Sig(bytes), metadata_import_, effective));
  ExpectEffectiveTypeIsVoid(effective);
}

TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeForValueTaskIsVoid) {
  // ValueTask is a struct, so it arrives as ELEMENT_TYPE_VALUETYPE rather than ELEMENT_TYPE_CLASS.
  const auto value_task = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                             WStr("System.Threading.Tasks.ValueTask"));
  ASSERT_NE(mdTypeRefNil, value_task);

  const auto bytes = SigBuilder().Byte(ELEMENT_TYPE_VALUETYPE).Token(value_task).Bytes();

  TypeSignature effective{};
  ASSERT_TRUE(TryGetRuntimeAsyncEffectiveReturnType(Sig(bytes), metadata_import_, effective));
  ExpectEffectiveTypeIsVoid(effective);
}

TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeForGenericTaskIsTheTypeArgument) {
  const auto task = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                       WStr("System.Threading.Tasks.Task`1"));
  ASSERT_NE(mdTypeRefNil, task);

  const auto bytes = SigBuilder()
                         .Byte(ELEMENT_TYPE_GENERICINST)
                         .Byte(ELEMENT_TYPE_CLASS)
                         .Token(task)
                         .Byte(1)
                         .Byte(ELEMENT_TYPE_I4)
                         .Bytes();

  TypeSignature effective{};
  ASSERT_TRUE(TryGetRuntimeAsyncEffectiveReturnType(Sig(bytes), metadata_import_, effective));

  EXPECT_EQ(bytes.data(), effective.pbBase);
  EXPECT_EQ(bytes.size() - 1, effective.offset);
  EXPECT_EQ(1u, effective.length);
  EXPECT_EQ(ELEMENT_TYPE_I4, effective.pbBase[effective.offset]);
}

TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeForGenericValueTaskIsTheTypeArgument) {
  const auto value_task = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                             WStr("System.Threading.Tasks.ValueTask`1"));
  ASSERT_NE(mdTypeRefNil, value_task);

  const auto bytes = SigBuilder()
                         .Byte(ELEMENT_TYPE_GENERICINST)
                         .Byte(ELEMENT_TYPE_VALUETYPE)
                         .Token(value_task)
                         .Byte(1)
                         .Byte(ELEMENT_TYPE_STRING)
                         .Bytes();

  TypeSignature effective{};
  ASSERT_TRUE(TryGetRuntimeAsyncEffectiveReturnType(Sig(bytes), metadata_import_, effective));

  EXPECT_EQ(1u, effective.length);
  EXPECT_EQ(ELEMENT_TYPE_STRING, effective.pbBase[effective.offset]);
}

// The cases above build their own blobs. This one takes the return signature of a real method in
// the sample library, so the whole path - blob layout, TypeRef resolution, slicing - runs against
// metadata we did not hand-craft.
TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeForARealTaskOfTMethod) {
  auto function = FunctionToTest(WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"),
                                 WStr("StayAndLayDown"));
  ASSERT_TRUE(function.IsValid());
  ASSERT_EQ(S_OK, function.method_signature.TryParse());

  // async Task<Biscuit<T1>> StayAndLayDown<TM1, TM2>(...)
  TypeSignature effective{};
  ASSERT_TRUE(TryGetRuntimeAsyncEffectiveReturnType(function.method_signature.GetReturnValue(),
                                                    metadata_import_, effective));

  // The unwrapped Biscuit<T1>, itself a generic instantiation - so the slice length has to come
  // from a real type walk rather than from assuming a single byte.
  const auto [element_type, type_flags] = effective.GetElementTypeAndFlags();
  EXPECT_EQ(static_cast<unsigned>(ELEMENT_TYPE_GENERICINST), element_type);
  EXPECT_EQ(0, type_flags & TypeFlagVoid);
  EXPECT_GT(effective.length, 1u);
}

TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeDeclinesRealNonTaskReturns) {
  // MethodImplAttributes.Async can be set on a method it has no effect on. Those must be declined
  // rather than guessed at - a wrong guess emits exactly the invalid IL we are trying to avoid.
  const std::vector<std::tuple<shared::WSTRING, shared::WSTRING>> methods = {
      {WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("Silence")},                 // void
      {WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("TellMeIfTheCookieIsYummy")}, // string
      {WStr("Samples.ExampleLibrary.FakeClient.DogClient`2"), WStr("Rollover")},                 // Biscuit
  };

  for (auto& [type_name, method_name] : methods) {
    auto function = FunctionToTest(type_name, method_name);
    ASSERT_TRUE(function.IsValid()) << "Failed method is : " << shared::ToString(method_name) << std::endl;
    ASSERT_EQ(S_OK, function.method_signature.TryParse());

    TypeSignature effective{};
    EXPECT_FALSE(TryGetRuntimeAsyncEffectiveReturnType(function.method_signature.GetReturnValue(),
                                                       metadata_import_, effective))
        << "Failed method is : " << shared::ToString(method_name) << std::endl;
  }
}

// A generic type that is not Task`1/ValueTask`1 must be declined even though it parses cleanly.
TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeDeclinesOtherGenericTypes) {
  const auto other = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                        WStr("System.Collections.Generic.List`1"));
  ASSERT_NE(mdTypeRefNil, other);

  const auto bytes = SigBuilder()
                         .Byte(ELEMENT_TYPE_GENERICINST)
                         .Byte(ELEMENT_TYPE_CLASS)
                         .Token(other)
                         .Byte(1)
                         .Byte(ELEMENT_TYPE_I4)
                         .Bytes();

  TypeSignature effective{};
  EXPECT_FALSE(TryGetRuntimeAsyncEffectiveReturnType(Sig(bytes), metadata_import_, effective));
}

// The name alone is not enough: Task is a class and ValueTask is a struct, so the element type has
// to agree with the name it resolves to. A type that merely borrows one of these names is spelled
// with the other element type and must be declined, not rewritten as though it were the real one.
TEST_F(CLRHelperTest, RuntimeAsyncEffectiveReturnTypeDeclinesAMismatchedElementType) {
  const auto task = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                       WStr("System.Threading.Tasks.Task"));
  const auto value_task = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                             WStr("System.Threading.Tasks.ValueTask"));
  const auto task_generic = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                               WStr("System.Threading.Tasks.Task`1"));
  const auto value_task_generic = GetOrDefineTypeRef(metadata_import_, metadata_emit_,
                                                     WStr("System.Threading.Tasks.ValueTask`1"));
  ASSERT_NE(mdTypeRefNil, task);
  ASSERT_NE(mdTypeRefNil, value_task);
  ASSERT_NE(mdTypeRefNil, task_generic);
  ASSERT_NE(mdTypeRefNil, value_task_generic);

  const std::vector<std::vector<COR_SIGNATURE>> mismatched = {
      // Task spelled as a struct
      SigBuilder().Byte(ELEMENT_TYPE_VALUETYPE).Token(task).Bytes(),
      // ValueTask spelled as a class
      SigBuilder().Byte(ELEMENT_TYPE_CLASS).Token(value_task).Bytes(),
      // Task`1 spelled as a struct
      SigBuilder()
          .Byte(ELEMENT_TYPE_GENERICINST)
          .Byte(ELEMENT_TYPE_VALUETYPE)
          .Token(task_generic)
          .Byte(1)
          .Byte(ELEMENT_TYPE_I4)
          .Bytes(),
      // ValueTask`1 spelled as a class
      SigBuilder()
          .Byte(ELEMENT_TYPE_GENERICINST)
          .Byte(ELEMENT_TYPE_CLASS)
          .Token(value_task_generic)
          .Byte(1)
          .Byte(ELEMENT_TYPE_I4)
          .Bytes(),
  };

  for (size_t i = 0; i < mismatched.size(); i++) {
    TypeSignature effective{};
    EXPECT_FALSE(TryGetRuntimeAsyncEffectiveReturnType(Sig(mismatched[i]), metadata_import_, effective))
        << "Signature at index " << i << " should not have been recognised" << std::endl;
  }
}