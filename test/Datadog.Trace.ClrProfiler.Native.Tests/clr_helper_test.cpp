#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"
#include "test_helpers.h"

using namespace trace;

class CLRHelperTest : public ::CLRHelperTestBase {};

TEST_F(CLRHelperTest, EnumeratesTypeDefs) {
  std::vector<std::wstring> expected_types = {
      L"Samples.ExampleLibrary.Class1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit`1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit",
      L"Samples.ExampleLibrary.FakeClient.DogClient`2",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1",
      L"Samples.ExampleLibrary.FakeClient.DogTrick",
      L"<>c",
      L"<StayAndLayDown>d__3`2"};

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
      L"System.Runtime", L"System.Diagnostics.Debug", L"System.Collections",
      L"System.Threading.Tasks"};
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
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  Integration i2 = {
      L"integration-2",
      {{{},
        {L"Assembly.Two", L"SomeType", L"SomeMethod", min_ver_, max_ver_, {}},
        {}}}};
  Integration i3 = {
      L"integration-3",
      {{{}, {L"System.Runtime", L"", L"", min_ver_, max_ver_, {}}, {}}}};
  std::vector<Integration> all = {i1, i2, i3};
  std::vector<Integration> expected = {i1, i3};
  std::vector<WSTRING> disabled_integrations = {"integration-2"_W};
  auto actual = FilterIntegrationsByName(all, disabled_integrations);
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FiltersIntegrationsByCaller) {
  Integration i1 = {
      L"integration-1",
      {{{L"Assembly.One", L"SomeType", L"SomeMethod", min_ver_, max_ver_, {}},
        {},
        {}}}};
  Integration i2 = {
      L"integration-2",
      {{{L"Assembly.Two", L"SomeType", L"SomeMethod", min_ver_, max_ver_, {}},
        {},
        {}}}};
  Integration i3 = {L"integration-3", {{{}, {}, {}}}};
  auto all = FlattenIntegrations({i1, i2, i3});
  auto expected = FlattenIntegrations({i1, i3});
  trace::AssemblyInfo assembly_info = {1, L"Assembly.One"};
  auto actual = FilterIntegrationsByCaller(all, assembly_info);
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FiltersIntegrationsByTarget) {
  Integration i1 = {L"integration-1",
                    {{{},
                      {L"Samples.ExampleLibrary",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  Integration i2 = {
      L"integration-2",
      {{{},
        {L"Assembly.Two", L"SomeType", L"SomeMethod", min_ver_, max_ver_, {}},
        {}}}};
  Integration i3 = {
      L"integration-3",
      {{{}, {L"System.Runtime", L"", L"", min_ver_, max_ver_, {}}, {}}}};
  auto all = FlattenIntegrations({i1, i2, i3});
  auto expected = FlattenIntegrations({i1, i3});
  auto actual = FilterIntegrationsByTarget(all, assembly_import_);
  EXPECT_EQ(actual, expected);
}

TEST_F(CLRHelperTest, FiltersFlattenedIntegrationMethodsByTarget) {
  MethodReference included = {L"Samples.ExampleLibrary",
                              L"SomeType",
                              L"SomeMethod",
                              min_ver_,
                              max_ver_,
                              {}};

  MethodReference excluded = {L"Samples.ExampleLibrary", L"SomeType",
                              L"SomeOtherMethod",        Version(0, 0, 0, 0),
                              Version(0, 1, 0, 0),       {}};

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
      L"<StayAndLayDown>d__3`2",
      L"Samples.ExampleLibrary.Class1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit",
      L"Samples.ExampleLibrary.FakeClient.Biscuit`1",
      L"Samples.ExampleLibrary.FakeClient.DogClient`2",
      L"Samples.ExampleLibrary.FakeClient.DogTrick",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1"};
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
      L"System.String",
      L"System.Threading.Tasks.Task",
      L"System.Threading.Tasks.Task`1",
      L"System.Tuple`2",
      L"System.Tuple`7",
      L"System.Type"};
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
      L"<StayAndLayDown>d__3`2",
      L"Samples.ExampleLibrary.Class1",
      L"Samples.ExampleLibrary.FakeClient.Biscuit",
      L"Samples.ExampleLibrary.FakeClient.Biscuit`1",
      L"Samples.ExampleLibrary.FakeClient.DogClient`2",
      L"Samples.ExampleLibrary.FakeClient.DogTrick",
      L"Samples.ExampleLibrary.FakeClient.DogTrick`1"};
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
