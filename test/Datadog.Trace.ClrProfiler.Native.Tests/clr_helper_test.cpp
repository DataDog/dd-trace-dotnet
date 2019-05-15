#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"

using namespace trace;

class DISABLED_CLRHelperTest : public ::testing::Test {
 protected:
  IMetaDataDispenser* metadata_dispenser_;
  ComPtr<IMetaDataImport2> metadata_import_;
  ComPtr<IMetaDataAssemblyImport> assembly_import_;
  Version min_ver_ = Version(0, 0, 0, 0);
  Version max_ver_ = Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX);

  void SetUp() override {
    ICLRMetaHost* metahost = nullptr;
    HRESULT hr;
    hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost,
                           (void**)&metahost);
    ASSERT_TRUE(SUCCEEDED(hr));

    IEnumUnknown* runtimes = nullptr;
    hr = metahost->EnumerateInstalledRuntimes(&runtimes);
    ASSERT_TRUE(SUCCEEDED(hr));

    ICLRRuntimeInfo* latest = nullptr;
    ICLRRuntimeInfo* runtime = nullptr;
    ULONG fetched = 0;
    while ((hr = runtimes->Next(1, (IUnknown**)&runtime, &fetched)) == S_OK &&
           fetched > 0) {
      latest = runtime;
    }

    hr =
        latest->GetInterface(CLSID_CorMetaDataDispenser, IID_IMetaDataDispenser,
                             (void**)&metadata_dispenser_);
    ASSERT_TRUE(SUCCEEDED(hr));

    ComPtr<IUnknown> metadataInterfaces;
    hr = metadata_dispenser_->OpenScope(L"Samples.ExampleLibrary.dll",
                                        ofReadWriteMask, IID_IMetaDataImport2,
                                        metadataInterfaces.GetAddressOf());
    ASSERT_TRUE(SUCCEEDED(hr)) << "Samples.ExampleLibrary.dll was not found.";

    metadata_import_ =
        metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2);
    assembly_import_ = metadataInterfaces.As<IMetaDataAssemblyImport>(
        IID_IMetaDataAssemblyImport);
  }
};

TEST_F(DISABLED_CLRHelperTest, EnumeratesTypeDefs) {
  std::vector<std::wstring> expected_types = {L"Samples.ExampleLibrary.Class1",
                                              L"<>c"};
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

TEST_F(DISABLED_CLRHelperTest, EnumeratesAssemblyRefs) {
  std::vector<std::wstring> expected_assemblies = {L"System.Runtime"};
  std::vector<std::wstring> actual_assemblies;
  for (auto& ref : EnumAssemblyRefs(assembly_import_)) {
    auto name = GetReferencedAssemblyMetadata(assembly_import_, ref).name;
    if (!name.empty()) {
      actual_assemblies.push_back(name);
    }
  }
  EXPECT_EQ(expected_assemblies, actual_assemblies);
}

TEST_F(DISABLED_CLRHelperTest, FiltersEnabledIntegrations) {
  Integration i1 = {L"integration-1",
                    {{{},
                      {L"Samples.ExampleLibrary",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  Integration i2 = {L"integration-2",
                    {{{},
                      {L"Assembly.Two",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  Integration i3 = {L"integration-3",
                    {{{},
                      {L"System.Runtime",
                       L"",
                       L"",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  std::vector<Integration> all = {i1, i2, i3};
  std::vector<Integration> expected = {i1, i3};
  std::vector<WSTRING> disabled_integrations = {"integration-2"_W};
  auto actual =
      FilterIntegrationsByName(all, disabled_integrations);
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, FiltersIntegrationsByCaller) {
  Integration i1 = {L"integration-1",
                    {{{L"Assembly.One",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {},
                      {}}}};
  Integration i2 = {L"integration-2",
                    {{{L"Assembly.Two",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {},
                      {}}}};
  Integration i3 = {L"integration-3", {{{}, {}, {}}}};
  auto all = FlattenIntegrations({i1, i2, i3});
  auto expected = FlattenIntegrations({i1, i3});
  trace::AssemblyInfo assembly_info = {1, L"Assembly.One"};
  auto actual =
      FilterIntegrationsByCaller(all, assembly_info);
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, FiltersIntegrationsByTarget) {
  Integration i1 = {L"integration-1",
                    {{{},
                      {L"Samples.ExampleLibrary",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  Integration i2 = {L"integration-2",
                    {{{},
                      {L"Assembly.Two",
                       L"SomeType",
                       L"SomeMethod",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  Integration i3 = {L"integration-3",
                    {{{},
                      {L"System.Runtime",
                       L"",
                       L"",
                       min_ver_,
                       max_ver_,
                       {}},
                      {}}}};
  auto all = FlattenIntegrations({i1, i2, i3});
  auto expected = FlattenIntegrations({i1, i3});
  auto actual =
      FilterIntegrationsByTarget(all, assembly_import_);
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromTypeDefs) {
  std::set<std::wstring> expected = {L"Samples.ExampleLibrary.Class1", L"<>c"};
  std::set<std::wstring> actual;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_def);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromTypeRefs) {
  std::set<std::wstring> expected = {
      L"System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
      L"System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
      L"System.Diagnostics.DebuggableAttribute",
      L"DebuggingModes",
      L"System.Runtime.Versioning.TargetFrameworkAttribute",
      L"System.Reflection.AssemblyCompanyAttribute",
      L"System.Reflection.AssemblyConfigurationAttribute",
      L"System.Reflection.AssemblyFileVersionAttribute",
      L"System.Reflection.AssemblyInformationalVersionAttribute",
      L"System.Reflection.AssemblyProductAttribute",
      L"System.Reflection.AssemblyTitleAttribute",
      L"System.Object",
      L"System.Func`3",
      L"System.Runtime.CompilerServices.CompilerGeneratedAttribute"};
  std::set<std::wstring> actual;
  for (auto& type_ref : EnumTypeRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_ref);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromModuleRefs) {
  // TODO(cbd): figure out how to create a module ref, for now its empty
  std::set<std::wstring> expected = {};
  std::set<std::wstring> actual;
  for (auto& module_ref : EnumModuleRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, module_ref);
    actual.insert(type_info.name);
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromMethods) {
  std::set<std::wstring> expected = {L"Samples.ExampleLibrary.Class1", L"<>c"};
  std::set<std::wstring> actual;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    for (auto& method_def : EnumMethods(metadata_import_, type_def)) {
      auto type_info = GetTypeInfo(metadata_import_, method_def);
      if (type_info.IsValid()) {
        actual.insert(type_info.name);
      }
    }
  }
  EXPECT_EQ(expected, actual);
}
