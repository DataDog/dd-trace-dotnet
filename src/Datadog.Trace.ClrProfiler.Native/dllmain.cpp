// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.

#include "ClassFactory.h"

const IID IID_IUnknown = {0x00000000,
                          0x0000,
                          0x0000,
                          {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

const IID IID_IClassFactory = {
    0x00000001,
    0x0000,
    0x0000,
    {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

BOOL STDMETHODCALLTYPE DllMain(HMODULE hModule, DWORD ul_reason_for_call,
                               LPVOID lpReserved) {
  return TRUE;
}

extern "C" HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid,
                                                       REFIID riid,
                                                       LPVOID* ppv) {
  // {846F5F1C-F9AE-4B07-969E-05C26BC060D8}
  const GUID CLSID_CorProfiler = {
      0x846f5f1c,
      0xf9ae,
      0x4b07,
      {0x96, 0x9e, 0x5, 0xc2, 0x6b, 0xc0, 0x60, 0xd8}};

  if (ppv == NULL || rclsid != CLSID_CorProfiler) {
    return E_FAIL;
  }

  auto factory = new ClassFactory;

  if (factory == NULL) {
    return E_FAIL;
  }

  return factory->QueryInterface(riid, ppv);
}

extern "C" HRESULT STDMETHODCALLTYPE DllCanUnloadNow() { return S_OK; }
