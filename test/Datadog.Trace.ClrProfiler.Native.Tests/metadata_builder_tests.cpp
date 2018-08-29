#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/clr_helpers.h"
#include "../../src/Datadog.Trace.ClrProfiler.Native/metadata_builder.h"

using namespace trace;

class MetadataBuilderTest : public ::testing::Test {
 protected:
  ModuleMetadata* module_metadata_;
  MetadataBuilder* metadata_builder_;
  ICLRStrongName* strong_name_;
  IMetaDataDispenser* metadata_dispenser_;

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

    hr = latest->GetInterface(CLSID_CLRStrongName, IID_ICLRStrongName,
                              (void**)&strong_name_);
    ASSERT_TRUE(SUCCEEDED(hr));

    ComPtr<IUnknown> metadataInterfaces;
    hr = metadata_dispenser_->OpenScope(L"Samples.ExampleLibrary.dll",
                                        ofReadWriteMask, IID_IMetaDataImport,
                                        metadataInterfaces.GetAddressOf());
    ASSERT_TRUE(SUCCEEDED(hr));

    const auto metadataImport =
        metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
    const auto metadataEmit =
        metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit);
    const auto assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(
        IID_IMetaDataAssemblyImport);
    const auto assemblyEmit =
        metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

    const std::wstring assemblyName = L"Samples.ExampleLibrary";
    std::vector<integration> integrations;
    module_metadata_ =
        new ModuleMetadata(metadataImport, assemblyName, integrations);

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
  method_reference ref1(L"", L"", L"", {});
  method_reference ref2(L"Samples.ExampleLibrary", L"Class1", L"Add", {});
  method_reference ref3(L"Samples.ExampleLibrary", L"Class1", L"Add", {});
  method_replacement mr1(ref1, ref2, ref3);
  auto hr = metadata_builder_->StoreWrapperMethodRef(mr1);
  ASSERT_EQ(S_OK, hr);

  mdMemberRef tmp;
  auto ok = module_metadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibrary]Class1.Add", tmp);
  EXPECT_TRUE(ok);
  EXPECT_NE(tmp, 0);

  tmp = 0;
  ok = module_metadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibrary]Class2.Add", tmp);
  EXPECT_FALSE(ok);
  EXPECT_EQ(tmp, 0);
}

TEST_F(MetadataBuilderTest, StoresWrapperMemberRefForSeparateAssembly) {
  method_reference ref1(L"", L"", L"", {});
  method_reference ref2(L"Samples.ExampleLibrary", L"Class1", L"Add", {});
  method_reference ref3(L"Samples.ExampleLibraryTracer", L"Class1", L"Add", {});
  method_replacement mr1(ref1, ref2, ref3);
  auto hr = metadata_builder_->StoreWrapperMethodRef(mr1);
  ASSERT_EQ(S_OK, hr);

  mdMemberRef tmp;
  auto ok = module_metadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibraryTracer]Class1.Add", tmp);
  EXPECT_TRUE(ok);
  EXPECT_NE(tmp, 0);
}