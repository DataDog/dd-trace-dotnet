// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.

#include "class_factory.h"
#include "cor_profiler.h"
#include "logging.h"
#include "version.h"

std::string ConvertToString(const IID guid) {
  char szGuid[40] = {0};
  sprintf_s(szGuid, "{%08X-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X}",
          guid.Data1, guid.Data2, guid.Data3, guid.Data4[0], guid.Data4[1],
          guid.Data4[2], guid.Data4[3], guid.Data4[4], guid.Data4[5],
          guid.Data4[6], guid.Data4[7]);
  return std::string(szGuid, 40);
}

ClassFactory::ClassFactory() : refCount(0) {}

ClassFactory::~ClassFactory() {}

HRESULT STDMETHODCALLTYPE ClassFactory::QueryInterface(REFIID riid,
                                                       void** ppvObject) {
  if (riid == IID_IUnknown || riid == IID_IClassFactory) {
    *ppvObject = this;
    this->AddRef();
    return S_OK;
  }

  *ppvObject = nullptr;
  return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE ClassFactory::AddRef() {
  return std::atomic_fetch_add(&this->refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE ClassFactory::Release() {
  int count = std::atomic_fetch_sub(&this->refCount, 1) - 1;
  if (count <= 0) {
    delete this;
  }

  return count;
}

// profiler entry point
HRESULT STDMETHODCALLTYPE ClassFactory::CreateInstance(IUnknown* pUnkOuter,
                                                       REFIID riid,
                                                       void** ppvObject) {
  if (pUnkOuter != nullptr) {
    *ppvObject = nullptr;
    return CLASS_E_NOAGGREGATION;
  }

  trace::Info("Datadog CLR Profiler ", PROFILER_VERSION,
              " on",

#ifdef _WIN32
              " Windows"
#elif MACOS
              " macOS"
#else
              " Linux"
#endif

#ifdef AMD64
            , " (amd64)"
#elif X86
            , " (x86)"
#elif ARM64
            , " (arm64)"
#elif ARM
            , " (arm)"
#endif
  );
  trace::Debug("ClassFactory::CreateInstance");

 #if defined(ARM64) || defined(ARM) || defined(AMD64)

  //
  // If the architecture is ARM64 or ARM, we check if the runtime is greater
  // than .NET 5.0 due in previous versions ReJIT is not fully supported.
  //

  // {CEC5B60E-C69C-495F-87F6-84D28EE16FFB}
  const IID UUID_ICorProfilerCallback10 = {
      0xcec5b60e,
      0xc69c,
      0x495f,
      {0x87, 0xf6, 0x84, 0xd2, 0x8e, 0xe1, 0x6f, 0xfb}};

  trace::Warn("Current riid: ", ConvertToString(riid));
  trace::Warn("Compare UUID_ICorProfilerCallback10: ",
              ConvertToString(UUID_ICorProfilerCallback10));

  if (riid != UUID_ICorProfilerCallback10) {
    trace::Warn("DATADOG TRACER DIAGNOSTICS - Failed to attach profiler: This architecture requires .NET 5.0 or greater.");
    return E_NOINTERFACE;
  }

 #endif

  auto profiler = new trace::CorProfiler();
  return profiler->QueryInterface(riid, ppvObject);
}

HRESULT STDMETHODCALLTYPE ClassFactory::LockServer(BOOL fLock) { return S_OK; }
