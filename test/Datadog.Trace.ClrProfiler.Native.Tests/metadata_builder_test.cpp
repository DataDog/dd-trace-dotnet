#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"
#include "../../src/Datadog.Trace.ClrProfiler.Native/metadata_builder.h"

using namespace trace;

class MetadataBuilderTest : public ::testing::Test {
 protected:
  ModuleMetadata* module_metadata_ = nullptr;
  MetadataBuilder* metadata_builder_ = nullptr;
  IMetaDataDispenser* metadata_dispenser_ = nullptr;
  std::vector<WSTRING> empty_sig_type_;

  void SetUp() override {
    ICLRMetaHost* metahost = nullptr;
    HRESULT hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost,
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
    ASSERT_TRUE(SUCCEEDED(hr)) << "File not found: Samples.ExampleLibrary.dll";

    const auto metadataImport =
        metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2);
    const auto metadataEmit =
        metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
    const auto assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(
        IID_IMetaDataAssemblyImport);
    const auto assemblyEmit =
        metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

    const std::wstring assemblyName = L"Samples.ExampleLibrary";

    const AppDomainID app_domain_id{};

    GUID module_version_id;
    metadataImport->GetScopeProps(NULL, 1024, nullptr, &module_version_id);

    const std::vector<IntegrationMethod> integrations;
    module_metadata_ =
        new ModuleMetadata(metadataImport, metadataEmit, assemblyImport, assemblyEmit, assemblyName, app_domain_id,
                           module_version_id, std::make_unique<std::vector<IntegrationMethod>>(integrations), NULL);

    mdModule module;
    hr = metadataImport->GetModuleFromScope(&module);
    ASSERT_TRUE(SUCCEEDED(hr));

    metadata_builder_ =
        new MetadataBuilder(*module_metadata_, module, metadataImport,
                            metadataEmit, assemblyImport, assemblyEmit);

    hr = metadata_builder_->EmitAssemblyRef(trace::AssemblyReference(
        L"Samples.ExampleLibraryTracer, Version=1.0.0.0"));
    ASSERT_TRUE(SUCCEEDED(hr));
  }

  void TearDown() override {
    delete this->module_metadata_;
    delete this->metadata_builder_;
  }
};

TEST_F(MetadataBuilderTest, StoresWrapperMemberRef) {

  const auto min_ver = Version(0, 0, 0, 0);
  const auto max_ver = Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX);
  const MethodReference ref1(L"", L"", L"", L"", min_ver, max_ver, {}, empty_sig_type_);
  const MethodReference ref2(L"Samples.ExampleLibrary", L"Class1", L"Add", L"", min_ver, max_ver, {}, empty_sig_type_);
  const MethodReference ref3(L"Samples.ExampleLibrary", L"Class1", L"Add", L"ReplaceTargetMethod", min_ver, max_ver, {}, empty_sig_type_);
  const MethodReplacement mr1(ref1, ref2, ref3);
  auto hr = metadata_builder_->StoreWrapperMethodRef(mr1);
  ASSERT_EQ(S_OK, hr);

  mdMemberRef tmp;
  auto key_failed = module_metadata_->IsFailedWrapperMemberKey(
      L"[Samples.ExampleLibrary]Class1.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535");
  auto ok = module_metadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibrary]Class1.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535", tmp);
  EXPECT_TRUE(ok);
  EXPECT_FALSE(key_failed);
  EXPECT_NE(tmp, 0);

  tmp = 0;
  key_failed = module_metadata_->IsFailedWrapperMemberKey(
      L"[Samples.ExampleLibrary]Class2.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535");
  ok = module_metadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibrary]Class2.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535", tmp);
  EXPECT_FALSE(ok);
  EXPECT_FALSE(key_failed);
  EXPECT_EQ(tmp, 0);
}

TEST_F(MetadataBuilderTest, StoresWrapperMemberRefForSeparateAssembly) {
  const auto min_ver = Version(0, 0, 0, 0);
  const auto max_ver = Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX);
  const MethodReference ref1(L"", L"", L"", L"", min_ver, max_ver, {},
                             empty_sig_type_);
  const MethodReference ref2(L"Samples.ExampleLibrary", L"Class1", L"Add", L"", min_ver, max_ver, {}, empty_sig_type_);
  const MethodReference ref3(L"Samples.ExampleLibraryTracer", L"Class1", L"Add", L"ReplaceTargetMethod",
                             min_ver, max_ver, {}, empty_sig_type_);
  const MethodReplacement mr1(ref1, ref2, ref3);
  auto hr = metadata_builder_->StoreWrapperMethodRef(mr1);
  ASSERT_EQ(S_OK, hr);

  mdMemberRef tmp;
  auto key_failed = module_metadata_->IsFailedWrapperMemberKey(L"[Samples.ExampleLibraryTracer]Class1.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535");
  auto ok = module_metadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibraryTracer]Class1.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535", tmp);
  EXPECT_TRUE(ok);
  EXPECT_FALSE(key_failed);
  EXPECT_NE(tmp, 0);
}

TEST_F(MetadataBuilderTest, StoresWrapperMemberRefRecordsFailure) {
  const auto min_ver = Version(0, 0, 0, 0);
  const auto max_ver = Version(USHRT_MAX, USHRT_MAX, USHRT_MAX, USHRT_MAX);
  const MethodReference ref1(L"", L"", L"", L"", min_ver, max_ver, {},
                             empty_sig_type_);
  const MethodReference ref2(L"Samples.ExampleLibrary", L"Class1", L"Add", L"", min_ver, max_ver, {}, empty_sig_type_);
  const MethodReference ref3(L"Samples.ExampleLibraryTracer.AssemblyDoesNotExist", L"Class1", L"Add", L"ReplaceTargetMethod",
                             min_ver, max_ver, {}, empty_sig_type_);
  const MethodReplacement mr1(ref1, ref2, ref3);
  auto hr = metadata_builder_->StoreWrapperMethodRef(mr1);
  ASSERT_NE(S_OK, hr);

  auto key_failed = module_metadata_->IsFailedWrapperMemberKey(L"[Samples.ExampleLibraryTracer.AssemblyDoesNotExist]Class1.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535");
  EXPECT_TRUE(key_failed);

  key_failed = module_metadata_->IsFailedWrapperMemberKey(L"[Samples.ExampleLibraryTracer]Class1.Add_vMin_0.0.0.0_vMax_65535.65535.65535.65535");
  EXPECT_FALSE(key_failed);
}