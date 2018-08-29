#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/iterators.h"

class IteratorTest : public ::testing::Test {
 protected:
  IMetaDataDispenser* metadata_dispenser_;
  ComPtr<IMetaDataImport> metadata_import_;
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
                                        ofReadWriteMask, IID_IMetaDataImport,
                                        metadataInterfaces.GetAddressOf());
    ASSERT_TRUE(SUCCEEDED(hr));

    metadata_import_ =
        metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
    assembly_import_ = metadataInterfaces.As<IMetaDataAssemblyImport>(
        IID_IMetaDataAssemblyImport);
  }
};

TEST_F(IteratorTest, EnumeratesTypeDefs) {
  std::vector<std::wstring> expected_types = {L"Samples.ExampleLibrary.Class1"};
  std::vector<std::wstring> actual_types;

  for (auto& def : trace::EnumTypeDefs(metadata_import_)) {
    std::wstring name(256, 0);
    unsigned long name_sz = 0;
    DWORD flags = 0;
    mdToken extends = 0;
    auto hr = metadata_import_->GetTypeDefProps(def, name.data(), 256, &name_sz,
                                                &flags, &extends);
    ASSERT_TRUE(SUCCEEDED(hr));

    if (name_sz > 0) {
      name = name.substr(0, name_sz - 1);
      actual_types.push_back(name);
    }
  }

  EXPECT_EQ(expected_types, actual_types);
}

TEST_F(IteratorTest, EnumeratesAssemblyRefs) {
  std::vector<std::wstring> expected_assemblies = {L"System.Runtime"};
  std::vector<std::wstring> actual_assemblies;
  for (auto& ref : trace::EnumAssemblyRefs(assembly_import_)) {
    const unsigned long name_max = 512;
    std::wstring name(name_max, 0);
    unsigned long name_sz = 0;
    ASSEMBLYMETADATA assembly_metadata{};
    DWORD flags = 0;
    auto hr = assembly_import_->GetAssemblyRefProps(
        ref, nullptr, nullptr, name.data(), name_max, &name_sz,
        &assembly_metadata, nullptr, nullptr, &flags);
    if (SUCCEEDED(hr)) {
      name = name.substr(0, name_sz - 1);
      actual_assemblies.push_back(name);
    }
  }
  EXPECT_EQ(expected_assemblies, actual_assemblies);
}
