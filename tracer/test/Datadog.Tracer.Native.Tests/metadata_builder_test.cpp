#include "pch.h"

#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "../../src/Datadog.Tracer.Native/metadata_builder.h"

#include "../../../shared/src/native-src/string.h"

using namespace trace;
using namespace shared;

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

    const std::vector<IntegrationDefinition> integrations;
    module_metadata_ =
        new ModuleMetadata(metadataImport, metadataEmit, assemblyImport, assemblyEmit, assemblyName, app_domain_id,
                           module_version_id, std::make_unique<std::vector<IntegrationDefinition>>(integrations), NULL, true, true);

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
