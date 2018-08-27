#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/iterators.h"
#include "../../src/Datadog.Trace.ClrProfiler.Native/metadata_builder.h"

class MetadataBuilderTest : public ::testing::Test {
 protected:
  ModuleMetadata* moduleMetadata_;
  MetadataBuilder* metadataBuilder_;

  void SetUp() override {
    ICLRMetaHost* metahost = NULL;
    HRESULT hr;
    hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost,
                           (VOID**)&metahost);
    ASSERT_FALSE(FAILED(hr));

    IEnumUnknown* runtimes = NULL;
    hr = metahost->EnumerateInstalledRuntimes(&runtimes);
    ASSERT_FALSE(FAILED(hr));

    ICLRRuntimeInfo* latest = NULL;
    ICLRRuntimeInfo* runtime = NULL;
    ULONG fetched = 0;
    while ((hr = runtimes->Next(1, (IUnknown**)&runtime, &fetched)) == S_OK &&
           fetched > 0) {
      latest = runtime;
    }

    IMetaDataDispenser* metadataDispenser = NULL;
    hr =
        latest->GetInterface(CLSID_CorMetaDataDispenser, IID_IMetaDataDispenser,
                             (VOID**)&metadataDispenser);
    ASSERT_FALSE(FAILED(hr));

    ComPtr<IUnknown> metadataInterfaces;
    hr = metadataDispenser->OpenScope(L"Samples.ExampleLibrary.dll",
                                      ofRead | ofWrite, IID_IMetaDataImport,
                                      metadataInterfaces.GetAddressOf());
    ASSERT_FALSE(FAILED(hr));

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
    moduleMetadata_ =
        new ModuleMetadata(metadataImport, assemblyName, integrations);

    mdModule module;
    hr = metadataImport->GetModuleFromScope(&module);
    ASSERT_FALSE(FAILED(hr));

    metadataBuilder_ =
        new MetadataBuilder(*moduleMetadata_, module, metadataImport,
                            metadataEmit, assemblyImport, assemblyEmit);
  }

  void TearDown() override {
    delete this->moduleMetadata_;
    delete this->metadataBuilder_;
  }
};

TEST_F(MetadataBuilderTest, StoresWrapperMemberRef) {
  method_reference ref1(L"", L"", L"", {});
  method_reference ref2(L"Samples.ExampleLibrary", L"Class1", L"Add", {});
  method_reference ref3(L"Samples.ExampleLibrary", L"Class1", L"Add", {});
  method_replacement mr1(ref1, ref2, ref3);
  auto hr = metadataBuilder_->store_wrapper_method_ref(mr1);
  ASSERT_EQ(S_OK, hr);

  mdMemberRef tmp;
  auto ok = moduleMetadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibrary]Class1.Add", tmp);
  EXPECT_TRUE(ok);
  EXPECT_NE(tmp, 0);

  tmp = 0;
  ok = moduleMetadata_->TryGetWrapperMemberRef(
      L"[Samples.ExampleLibrary]Class2.Add", tmp);
  EXPECT_FALSE(ok);
  EXPECT_EQ(tmp, 0);
}