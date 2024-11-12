#include "pch.h"

#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "test_helpers.h"
#include "../../../shared/src/native-src/pal.h"

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