#include "pch.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/iterators.h"

TEST(IteratorTests, LoopsOverAssemblyRefs) {
  ICLRMetaHost* metahost = nullptr;
  HRESULT hr;
  hr =
      CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (void**)&metahost);
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

  IMetaDataDispenser* metadata_dispenser;
  hr = latest->GetInterface(CLSID_CorMetaDataDispenser, IID_IMetaDataDispenser,
                            (void**)&metadata_dispenser);
  ASSERT_TRUE(SUCCEEDED(hr));

  ComPtr<IUnknown> metadataInterfaces;
  hr = metadata_dispenser->OpenScope(L"Samples.ExampleLibrary.dll",
                                      ofReadWriteMask, IID_IMetaDataImport,
                                      metadataInterfaces.GetAddressOf());
  ASSERT_TRUE(SUCCEEDED(hr));

  const auto metadata_import = metadataInterfaces.As<IMetaDataImport>(
      IID_IMetaDataAssemblyImport);


  for (auto& def : trace::EnumTypeDefs(metadata_import)) {
    EXPECT_EQ(123, def);
  }
}
