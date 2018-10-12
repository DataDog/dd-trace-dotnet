#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"

using namespace trace;

class DISABLED_CLRHelperTest : public ::testing::Test {
 protected:
  IMetaDataDispenser* metadata_dispenser_;
  ComPtr<IMetaDataImport2> metadata_import_;
  ComPtr<IMetaDataAssemblyImport> assembly_import_;

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
    ASSERT_TRUE(SUCCEEDED(hr));

    metadata_import_ =
        metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2);
    assembly_import_ = metadataInterfaces.As<IMetaDataAssemblyImport>(
        IID_IMetaDataAssemblyImport);
  }
};

TEST_F(DISABLED_CLRHelperTest, EnumeratesTypeDefs) {
  std::vector<std::u16string> expected_types = {
      u"Samples.ExampleLibrary.Class1", u"<>c"};
  std::vector<std::u16string> actual_types;

  for (auto& def : EnumTypeDefs(metadata_import_)) {
    std::u16string name(256, 0);
    DWORD name_sz = 0;
    DWORD flags = 0;
    mdToken extends = 0;
    auto hr = metadata_import_->GetTypeDefProps(
        def, ToLPWSTR(name), (DWORD)(name.size()), &name_sz, &flags, &extends);
    ASSERT_TRUE(SUCCEEDED(hr));

    if (name_sz > 0) {
      name = name.substr(0, name_sz - 1);
      actual_types.push_back(name);
    }
  }

  EXPECT_EQ(expected_types, actual_types);
}

TEST_F(DISABLED_CLRHelperTest, EnumeratesAssemblyRefs) {
  std::vector<std::u16string> expected_assemblies = {u"System.Runtime"};
  std::vector<std::u16string> actual_assemblies;
  for (auto& ref : EnumAssemblyRefs(assembly_import_)) {
    auto name = GetAssemblyName(assembly_import_, ref);
    if (!name.empty()) {
      actual_assemblies.push_back(name);
    }
  }
  EXPECT_EQ(expected_assemblies, actual_assemblies);
}

TEST_F(DISABLED_CLRHelperTest, FiltersIntegrationsByCaller) {
  Integration i1 = {
      u"integration-1",
      {{{u"Assembly.One", u"SomeType", u"SomeMethod", {}}, {}, {}}}};
  Integration i2 = {
      u"integration-2",
      {{{u"Assembly.Two", u"SomeType", u"SomeMethod", {}}, {}, {}}}};
  Integration i3 = {u"integration-3", {{{}, {}, {}}}};
  std::vector<Integration> all = {i1, i2, i3};
  std::vector<Integration> expected = {i1, i3};
  std::vector<Integration> actual =
      FilterIntegrationsByCaller(all, u"Assembly.One");
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, FiltersIntegrationsByTarget) {
  Integration i1 = {
      u"integration-1",
      {{{}, {u"Samples.ExampleLibrary", u"SomeType", u"SomeMethod", {}}, {}}}};
  Integration i2 = {
      u"integration-2",
      {{{}, {u"Assembly.Two", u"SomeType", u"SomeMethod", {}}, {}}}};
  Integration i3 = {u"integration-3",
                    {{{}, {u"System.Runtime", u"", u"", {}}, {}}}};
  std::vector<Integration> all = {i1, i2, i3};
  std::vector<Integration> expected = {i1, i3};
  std::vector<Integration> actual =
      FilterIntegrationsByTarget(all, assembly_import_);
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromTypeDefs) {
  std::set<std::u16string> expected = {u"Samples.ExampleLibrary.Class1",
                                       u"<>c"};
  std::set<std::u16string> actual;
  for (auto& type_def : EnumTypeDefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, type_def);
    if (type_info.IsValid()) {
      actual.insert(type_info.name);
    }
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromTypeRefs) {
  std::set<std::u16string> expected = {
      u"System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
      u"System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
      u"System.Diagnostics.DebuggableAttribute",
      u"DebuggingModes",
      u"System.Runtime.Versioning.TargetFrameworkAttribute",
      u"System.Reflection.AssemblyCompanyAttribute",
      u"System.Reflection.AssemblyConfigurationAttribute",
      u"System.Reflection.AssemblyFileVersionAttribute",
      u"System.Reflection.AssemblyInformationalVersionAttribute",
      u"System.Reflection.AssemblyProductAttribute",
      u"System.Reflection.AssemblyTitleAttribute",
      u"System.Object",
      u"System.Func`3",
      u"System.Runtime.CompilerServices.CompilerGeneratedAttribute"};
  std::set<std::u16string> actual;
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
  std::set<std::u16string> expected = {};
  std::set<std::u16string> actual;
  for (auto& module_ref : EnumModuleRefs(metadata_import_)) {
    auto type_info = GetTypeInfo(metadata_import_, module_ref);
    actual.insert(type_info.name);
  }
  EXPECT_EQ(expected, actual);
}

TEST_F(DISABLED_CLRHelperTest, GetsTypeInfoFromMethods) {
  std::set<std::u16string> expected = {u"Samples.ExampleLibrary.Class1",
                                       u"<>c"};
  std::set<std::u16string> actual;
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
