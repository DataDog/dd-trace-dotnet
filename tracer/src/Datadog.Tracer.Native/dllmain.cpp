// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.

#include "dllmain.h"
#include "class_factory.h"
#include "logger.h"

#ifndef _WIN32
#undef EXTERN_C
#define EXTERN_C extern "C" __attribute__((visibility("default")))
#endif

const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

const IID IID_IClassFactory = {0x00000001, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

HINSTANCE DllHandle;
thread_local bool _dummyTLSUsage;

EXTERN_C BOOL STDMETHODCALLTYPE DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            trace::Logger::Info("Tracer CLRProfiler DLL loaded.");
            trace::Logger::Info("Pointer size: ", 8 * sizeof(void*), " bits.");
            break;

        case DLL_PROCESS_DETACH:
            trace::Logger::Info("Tracer CLRProfiler DLL unloaded.");
            trace::Logger::Info("Reading from the TLS variable: ", _dummyTLSUsage);
            break;
    }

    DllHandle = hModule;
    return TRUE;
}

EXTERN_C HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    // {846F5F1C-F9AE-4B07-969E-05C26BC060D8}
    const GUID CLSID_CorProfiler = {0x846f5f1c, 0xf9ae, 0x4b07, {0x96, 0x9e, 0x5, 0xc2, 0x6b, 0xc0, 0x60, 0xd8}};

    // {50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5}
    const GUID CLSID_New_CorProfiler = {0x50da5eed, 0xf1ed, 0xb00b, {0x10, 0x55, 0x5a, 0xfe, 0x55, 0xa1, 0xad, 0xe5}};

    // Dummy usage of a TLS variable.
    _dummyTLSUsage = true;
    trace::Logger::Info("Writing to the TLS variable: ", _dummyTLSUsage);

    if (ppv == nullptr || (rclsid != CLSID_CorProfiler && rclsid != CLSID_New_CorProfiler))
    {
        return E_FAIL;
    }

    auto factory = new ClassFactory;

    if (factory == nullptr)
    {
        return E_FAIL;
    }

    return factory->QueryInterface(riid, ppv);
}

EXTERN_C HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    trace::Logger::Info("Reading from the TLS variable: ", _dummyTLSUsage);
    return S_OK;
}