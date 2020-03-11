#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"
#include "test_helpers.h"

using namespace trace;

class CLRHelperTest : public ::CLRHelperTestBase {};

TEST_F(CLRHelperTest, EnumeratesTypeDefs) {
  std::vector<std::wstring> expected_types = {
      L"Samples.ExampleLibrary.Class1",
      L"Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2",
      L"Samples.ExampleLibrary.GenericTests.GenericTarget`2",
      L"Samples.ExampleLibrary.GenericTests.PointStruct",
      L"Samples.ExampleLibrary.GenericTests.StructContainer`1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit`1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit",
      L"Samples.ExampleLibrary.FakeClient.DogClient`2",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1",
      L"Samples.ExampleLibrary.FakeClient.DogTrick",
      L"<>c",
      L"Cookie",
      L"<StayAndLayDown>d__4`2",
      L"Raisin"};

  std::vector<std::wstring> actual_types;

  for (auto& def : EnumTypeDefs(metadata_import_)) {
    std::wstring name(256, 0);
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
  std::vector<std::wstring> expected_assemblies = {
      L"System.Runtime",
      L"System.Collections",
      L"System.Threading.Tasks",
      L"System.Diagnostics.Debug"};
  std::vector<std::wstring> actual_assemblies;
  for (auto& ref : EnumAssemblyRefs(assembly_import_)) {
    auto name = GetReferencedAssemblyMetadata(assembly_import_, ref).name;
    if (!name.empty()) {
      actual_assemblies.push_back(name);
    }
  }
  EXPECT_EQ(expected_assemblies, actual_assemblies);
}

TEST_F(CLRHelperTest, FiltersEnabledIntegrations) {
  Integration i1 = {L"integration-1",
                    {{{},
                      {L"Samples.ExampleLibrary",
                       L"SomeType",
                       L"SomeMethod",
                       L"ReplaceTargetMethod",
                       min_ver_,
                       max_ver_,
                       {},
                       empty_sig_type_},
                      {}}}};
  Integration i2 = {
      L"integration-2",
      {{{},
        {L"Assembly.Two", L"SomeType", L"SomeMethod", L"ReplaceTargetMethod", min_ver_, max_ver_, {}, empty_sig_type_},
        {}}}};
  Integration i3 = {
      L"integration-3",
      {{{}, {L"System.Runtime", L"", L"", L"ReplaceTargetMethod", min_ver_, max_ver_, {}, empty_sig_type_}, {}}}};
  std::vector<Integration> all = {i1, i2, i3};
  std::vector<Integration> expected = {i1, i3};
  std::vector<WSTRING> disabled_integrations = {"integration-2"_W};
  auto actual = FilterIntegrationsByName(all, disabled_integrations);
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FiltersIntegrationsByCaller) {
  Integration i1 = {
      L"integration-1",
      {{{L"Assembly.One", L"SomeType", L"SomeMethod", L"ReplaceTargetMethod", min_ver_, max_ver_, {}, empty_sig_type_},
        {},
        {}}}};
  Integration i2 = {
      L"integration-2",
      {{{L"Assembly.Two", L"SomeType", L"SomeMethod", L"ReplaceTargetMethod", min_ver_, max_ver_, {}, empty_sig_type_},
        {},
        {}}}};
  Integration i3 = {L"integration-3", {{{}, {}, {}}}};
  auto all = FlattenIntegrations({i1, i2, i3});
  auto expected = FlattenIntegrations({i1, i3});
  ModuleID manifest_module_id{};
  AppDomainID app_domain_id{};
  trace::AssemblyInfo assembly_info = { 1, L"Assembly.One", manifest_module_id,  app_domain_id, L"AppDomain1"};
  auto actual = FilterIntegrationsByCaller(all, assembly_info);
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FiltersIntegrationsByTarget) {
  Integration i1 = {L"integration-1",
                    {{{},
                      {L"Samples.ExampleLibrary",
                       L"SomeType",
                       L"SomeMethod",
                       L"ReplaceTargetMethod",
                       min_ver_,
                       max_ver_,
                       {},
                       empty_sig_type_},
                      {}}}};
  Integration i2 = {
      L"integration-2",
      {{{},
        {L"Assembly.Two", L"SomeType", L"SomeMethod", L"ReplaceTargetMethod", min_ver_, max_ver_, {}, empty_sig_type_},
        {}}}};
  Integration i3 = {
      L"integration-3",
      {{{}, {L"System.Runtime", L"", L"", L"ReplaceTargetMethod", min_ver_, max_ver_, {}, empty_sig_type_}, {}}}};
  auto all = FlattenIntegrations({i1, i2, i3});
  auto expected = FlattenIntegrations({i1, i3});
  auto actual = FilterIntegrationsByTarget(all, assembly_import_);
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FiltersFlattenedIntegrationMethodsByTarget) {
  MethodReference included = {L"Samples.ExampleLibrary",
                              L"SomeType",
                              L"SomeMethod",
                              L"ReplaceTargetMethod",
                              min_ver_,
                              max_ver_,
                              {},
                              empty_sig_type_};

  MethodReference excluded = {L"Samples.ExampleLibrary",
                              L"SomeType",
                              L"SomeOtherMethod",
                              L"ReplaceTargetMethod",
                              Version(0, 0, 0, 0),
                              Version(0, 1, 0, 0),
                              {},
                              empty_sig_type_};

  Integration i1 = {L"integration-1", {{{}, included, {}}, {{}, excluded, {}}}};
  auto all = FlattenIntegrations({i1});
  auto filtered = FilterIntegrationsByTarget(all, assembly_import_);
  bool foundExclusion = false;
  for (auto& item : filtered) {
    if (item.replacement.target_method == excluded) {
      foundExclusion = true;
    }
  }
  EXPECT_FALSE(foundExclusion)
      << "Expected method within integration to be filtered by version.";
}

TEST_F(CLRHelperTest, GetsTypeInfoFromTypeDefs) {
  std::set<std::wstring> expected = {
      L"<>c",
      L"<StayAndLayDown>d__4`2",
      L"Cookie",
      L"Raisin",
      L"Samples.ExampleLibrary.Class1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit",
      L"Samples.ExampleLibrary.FakeClient.Biscuit`1",
      L"Samples.ExampleLibrary.FakeClient.DogClient`2",
      L"Samples.ExampleLibrary.FakeClient.DogTrick",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1",
      L"Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2",
      L"Samples.ExampleLibrary.GenericTests.GenericTarget`2",
      L"Samples.ExampleLibrary.GenericTests.PointStruct",
      L"Samples.ExampleLibrary.GenericTests.StructContainer`1"};
  std::set<std::wstring> actual;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_def);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromTypeRefs) {
  std::set<std::wstring> expected = {
      L"DebuggingModes",
      L"Enumerator",
      L"System.Array",
      L"System.Collections.DictionaryEntry",
      L"System.Collections.Generic.Dictionary`2",
      L"System.Collections.Generic.IList`1",
      L"System.Collections.Generic.List`1",
      L"System.Diagnostics.DebuggableAttribute",
      L"System.Diagnostics.DebuggerBrowsableAttribute",
      L"System.Diagnostics.DebuggerBrowsableState",
      L"System.Diagnostics.DebuggerHiddenAttribute",
      L"System.Diagnostics.DebuggerStepThroughAttribute",
      L"System.Exception",
      L"System.Func`3",
      L"System.Guid",
      L"System.Int32",
      L"System.Object",
      L"System.Reflection.AssemblyCompanyAttribute",
      L"System.Reflection.AssemblyConfigurationAttribute",
      L"System.Reflection.AssemblyFileVersionAttribute",
      L"System.Reflection.AssemblyInformationalVersionAttribute",
      L"System.Reflection.AssemblyProductAttribute",
      L"System.Reflection.AssemblyTitleAttribute",
      L"System.Runtime.CompilerServices.AsyncStateMachineAttribute",
      L"System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1",
      L"System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
      L"System.Runtime.CompilerServices.CompilerGeneratedAttribute",
      L"System.Runtime.CompilerServices.IAsyncStateMachine",
      L"System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
      L"System.Runtime.CompilerServices.TaskAwaiter",
      L"System.Runtime.CompilerServices.TaskAwaiter`1",
      L"System.Runtime.Versioning.TargetFrameworkAttribute",
      L"System.RuntimeTypeHandle",
      L"System.String",
      L"System.Threading.Tasks.Task",
      L"System.Threading.Tasks.Task`1",
      L"System.Tuple`2",
      L"System.Tuple`7",
      L"System.Type",
      L"System.ValueType"};
  std::set<std::wstring> actual;
  for (auto& type_ref : EnumTypeRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_ref);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromModuleRefs) {
  // TODO(cbd): figure out how to create a module ref, for now its empty
  std::set<std::wstring> expected = {};
  std::set<std::wstring> actual;
  for (auto& module_ref : EnumModuleRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, module_ref);
    actual.insert(type_info.name);
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, GetsTypeInfoFromMethods) {
  std::set<std::wstring> expected = {
      L"<>c",
      L"<StayAndLayDown>d__4`2",
      L"Cookie",
      L"Raisin",
      L"Samples.ExampleLibrary.Class1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit",
      L"Samples.ExampleLibrary.FakeClient.Biscuit`1",
      L"Samples.ExampleLibrary.FakeClient.DogClient`2",
      L"Samples.ExampleLibrary.FakeClient.DogTrick",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1",
      L"Samples.ExampleLibrary.GenericTests.ComprehensiveCaller`2",
      L"Samples.ExampleLibrary.GenericTests.GenericTarget`2",
      L"Samples.ExampleLibrary.GenericTests.PointStruct",
      L"Samples.ExampleLibrary.GenericTests.StructContainer`1"};
  std::set<std::wstring> actual;
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

TEST_F(CLRHelperTest,
       ReturnTypeIsValueTypeOrGenericReturnsCorrectlyForMethodDefs) {
  std::set<std::pair<std::wstring, std::wstring>> expected = {
      {L".ctor", L""},
      {L"Add", L"System.Int32"},
      {L"Multiply", L"System.Int32"},
      {L"ToCustomString", L""},
      {L"ToObject", L""},
      {L"ToArray", L""},
      {L"ToCustomArray", L""},
      {L"ToMdArray", L""},
      {L"ToJaggedArray", L""},
      {L"ToList", L""},
      {L"ToEnumerator", L"Enumerator"},
      {L"ToDictionaryEntry", L"System.Collections.DictionaryEntry"},

      // Primitive tests
      {L"ToBool", L"System.Boolean"},
      {L"ToChar", L"System.Char"},
      {L"ToSByte", L"System.SByte"},
      {L"ToByte", L"System.Byte"},
      {L"ToInt16", L"System.Int16"},
      {L"ToUInt16", L"System.UInt16"},
      {L"ToInt32", L"System.Int32"},
      {L"ToUInt32", L"System.UInt32"},
      {L"ToInt64", L"System.Int64"},
      {L"ToUInt64", L"System.UInt64"},
      {L"ToSingle", L"System.Single"},
      {L"ToDouble", L"System.Double"}};
  std::set<std::pair<std::wstring, std::wstring>> actual;

  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    for (auto& method_def : EnumMethods(metadata_import_, type_def)) {
      auto type_info = GetTypeInfo(metadata_import_, method_def);
      if (type_info.IsValid() &&
          type_info.name == L"Samples.ExampleLibrary.Class1") {
        mdToken type_token = mdTokenNil;
        auto target = GetFunctionInfo(metadata_import_, method_def);
        bool result = ReturnTypeIsValueTypeOrGeneric(metadata_import_,
                                                     metadata_emit_,
                                                     assembly_emit_,
                                                     target.id,
                                                     target.signature,
                                                     &type_token) &&
                      type_token != mdTokenNil;
        if (result) {
          auto new_type_ref_info = GetTypeInfo(metadata_import_, type_token);
          actual.insert({target.name, new_type_ref_info.name});
        } else {
          actual.insert({target.name, L""});
        }
      }
    }
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest,
       ReturnTypeIsValueTypeOrGenericReturnsCorrectlyForMemberRefs) {
  std::vector<std::pair<std::wstring, bool>> expected = {
      {L"ReturnT1", true},
      {L"ReturnT1", true},
      {L"ReturnT1", false},
      {L"ReturnT1", true},
      {L"ReturnT1", false},
      {L"ReturnT1", true},

      {L"ReturnT2", true},
      {L"ReturnT2", true},
      {L"ReturnT2", false},
      {L"ReturnT2", true},
      {L"ReturnT2", false},
      {L"ReturnT2", true}};
  std::vector<std::pair<std::wstring, bool>> actual;

  for (mdMemberRef current = mdtMemberRef + 1; metadata_import_->IsValidToken(current); current++) {
    auto target = GetFunctionInfo(metadata_import_, current);
    if (target.name == L"ReturnT1" || target.name == L"ReturnT2") {
      mdToken type_token = mdTokenNil;
      bool result = ReturnTypeIsValueTypeOrGeneric(metadata_import_,
                                                  metadata_emit_,
                                                  assembly_emit_,
                                                  target.id,
                                                  target.signature,
                                                  &type_token) &&
                    type_token != mdTokenNil;
      actual.push_back({target.name, result});
    }
  }
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest,
       ReturnTypeIsValueTypeOrGenericReturnsCorrectlyForMethodSpecs) {
  std::vector<std::pair<std::wstring, bool>> expected = {
      {L"ReturnM1", true},
      {L"ReturnM1", true},
      {L"ReturnM1", false},
      {L"ReturnM1", true},
      {L"ReturnM1", false},
      {L"ReturnM1", true},

      {L"ReturnM2", true},
      {L"ReturnM2", true},
      {L"ReturnM2", false},
      {L"ReturnM2", true},
      {L"ReturnM2", false},
      {L"ReturnM2", true}};
  std::vector<std::pair<std::wstring, bool>> actual;

  for (mdMethodSpec current = mdtMethodSpec + 1; metadata_import_->IsValidToken(current); current++) {
    mdToken type_token = mdTokenNil;
    auto target = GetFunctionInfo(metadata_import_, current);
    if (target.name.find(L"ReturnM") != std::string::npos) {
      bool result = ReturnTypeIsValueTypeOrGeneric(metadata_import_,
                                                   metadata_emit_,
                                                   assembly_emit_,
                                                   target.id,
                                                   target.signature,
                                                   &type_token) &&
                    type_token != mdTokenNil;
      actual.push_back({target.name, result});
    }
  }
  EXPECT_EQ(actual, expected);
}
